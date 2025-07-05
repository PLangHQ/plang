using LightInject;
using PLang.Building.Model;
using PLang.Container;
using PLang.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PLang.Services.OutputStream;

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
			//todo: dont really understand this SemaphoreSlim, seems to work
			_semaphore = new SemaphoreSlim(maxSize, maxSize);
			
			for (int i = 0; i < initialSize; i++)
			{
				var engine = _factory();
				SetProperties(engine, engine.ParentEngine, null, engine.Name, engine.OutputStreamFactory.CreateHandler());

				_pool.Add(engine);
				//Interlocked.Increment(ref _currentCount);
			}
		}

		public async Task<IEngine> RentAsync(IEngine parentEngine, GoalStep? callingStep, string name, IOutputStream? outputStream = null)
		{
			if (_pool.TryTake(out var engine))
			{
				return SetProperties(engine, parentEngine, callingStep, name, outputStream);
			}

			var newEngine = _factory();
			return SetProperties(newEngine, parentEngine, callingStep, name, outputStream);
		}

		private IEngine SetProperties(IEngine engine, IEngine parentEngine, GoalStep? callingStep, string name, IOutputStream? outputStream)
		{
			engine.Name = name;
			engine.SetParentEngine(parentEngine);
			engine.FileSystem.AddFileAccess(new SafeFileSystem.FileAccessControl(parentEngine.Path, engine.FileSystem.OsDirectory, ProcessId: engine.Id));
			if (outputStream != null)
			{
				engine.SetOutputStream(outputStream);
			}
			if (callingStep != null)
			{
				engine.SetCallingStep(callingStep);
			}

			//engine.GetContext().Clear();
			foreach (var item in parentEngine.GetContext())
			{
				engine.GetContext().AddOrReplace(item.Key, item.Value);
			}

			//engine.GetMemoryStack().Clear();
			foreach (var item in parentEngine.GetMemoryStack().GetMemoryStack())
			{
				engine.GetMemoryStack().Put(item, callingStep);
			}
						
			return engine;
		}

		public void Return(IEngine engine)
		{
			engine.Return();

			_pool.Add(engine);
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
