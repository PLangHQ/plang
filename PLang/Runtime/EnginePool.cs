using LightInject;
using PLang.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Runtime
{
	public class EnginePool
	{
		private readonly ConcurrentBag<IEngine> _pool = new();
		private readonly SemaphoreSlim _semaphore;
		private readonly Func<IEngine> _factory;
		private readonly int _maxSize;
		private int _currentCount;
		IServiceContainer container;

		public EnginePool(int initialSize, Func<IEngine> factory, IServiceContainer container, int maxSize = 50, PLangAppContext? context = null)
		{
			if (initialSize > maxSize)
				throw new ArgumentException("Initial size cannot exceed max size.");

			_factory = factory ?? throw new ArgumentNullException(nameof(factory));
			_maxSize = maxSize;
			_semaphore = new SemaphoreSlim(initialSize, maxSize);
			this.container = container;

			for (int i = 0; i < initialSize; i++)
			{
				var engine = _factory();
				engine.Init(container, context);
				
				_pool.Add(engine);
				Interlocked.Increment(ref _currentCount);
			}
		}

		public async Task<IEngine> RentAsync()
		{
			await _semaphore.WaitAsync();

			if (_pool.TryTake(out var engine))
				return engine;

			// Create a new instance only if we haven't reached maxSize
			if (Interlocked.Increment(ref _currentCount) <= _maxSize) {
				var newEngine = _factory();
				newEngine.Init(container);
				return newEngine;
			}

			// If maxSize reached, decrement and wait for an available engine
			Interlocked.Decrement(ref _currentCount);
			return await RentAsync(); 
		}

		public void Return(IEngine engine)
		{
			_pool.Add(engine);
			_semaphore.Release();
		}
	}
}
