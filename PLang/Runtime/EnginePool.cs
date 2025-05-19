using LightInject;
using PLang.Container;
using PLang.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PLang.Runtime
{
	public class EnginePool : IDisposable
	{
		private readonly ConcurrentBag<IEngine> _pool = new();
		private readonly SemaphoreSlim _semaphore;
		private readonly Func<IEngine> _factory;
		private readonly int _maxSize;
		private int _currentCount;
		private bool disposed;

		public EnginePool(int initialSize, Func<IEngine> factory, int maxSize = 50)
		{
			if (initialSize > maxSize)
				throw new ArgumentException("Initial size cannot exceed max size.");

			_factory = factory ?? throw new ArgumentNullException(nameof(factory));
			_maxSize = maxSize;
			_semaphore = new SemaphoreSlim(maxSize, maxSize);
			
			for (int i = 0; i < initialSize; i++)
			{
				var engine = _factory();
				
				_pool.Add(engine);
				//Interlocked.Increment(ref _currentCount);
			}
		}

		public async Task<IEngine> RentAsync(string name)
		{
			if (_pool.TryTake(out var engine))
			{
				engine.Name = name;
				return engine;
			}

			var newEngine = _factory();
			newEngine.Name = name;
			return newEngine;
		}

		public void Return(IEngine engine)
		{
			_pool.Add(engine);

		//	_semaphore.Release();
			//Interlocked.Decrement(ref _currentCount);
		}

		public virtual void Dispose()
		{
			if (this.disposed)
			{
				return;
			}
			_semaphore.Dispose();
			this.disposed = true;
		}

		protected virtual void ThrowIfDisposed()
		{
			if (this.disposed)
			{
				throw new ObjectDisposedException(this.GetType().FullName);
			}
		}
	}
}
