using LightInject;
using PLang.Building.Model;
using PLang.Container;
using PLang.Errors.Handlers;
using PLang.Interfaces;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Xml.Linq;

namespace PLang.Runtime
{

	public class EnginePool
	{
		public List<string> engineIds = new();
		private ConcurrentStack<IEngine> _pool = new();
		private IEngine rootEngine;
		public ConcurrentStack<IEngine> Pool { get { return _pool; } }
		public EnginePool(IEngine rootEngine)
		{
			
			this.rootEngine = rootEngine;
		}

		private IEngine GetRootEngine()
		{
			var engine = rootEngine;
			var parentEngine = rootEngine.ParentEngine;
			while (parentEngine != null)
			{
				engine = parentEngine;
				parentEngine = engine.ParentEngine;
			}
			this.rootEngine = engine;
			return engine;
		}

		public async Task<IEngine> RentAsync(GoalStep callingStep)
		{
			var rootEngine = GetRootEngine();
			var enginePool = rootEngine.EnginePool;
			var pool = enginePool.Pool;

			if (pool.TryPop(out var engine))
			{
				engine.IsInPool = false;
				if (enginePool.engineIds.Contains(engine.Id))
				{
					enginePool.engineIds.Remove(engine.Id);
				}
				
				InitPerRequest(rootEngine.Container, engine);
				return engine;
			}

			engine = CreateEngine(rootEngine.FileSystem.RootDirectory, rootEngine.Container);

			return engine;
		}

		public void Return(IEngine engine, bool reset = false)
		{
			var rootEngine = GetRootEngine();
			var enginePool = rootEngine.EnginePool;
			var pool = enginePool.Pool;
			if (engine.IsInPool)
			{
				return;
			}
			engine.IsInPool = true;

			if (enginePool.engineIds.Contains(engine.Id))
			{
				Console.WriteLine($"RETURNING existing id: {engine.Id}");
				return;
			}

			engine.Reset(true);
			
			pool.Push(engine);
			enginePool.engineIds.Add(engine.Id);

			
		}


		public void CleanupEngines()
		{
			var rootEngine = GetRootEngine();
			var pool = rootEngine.EnginePool.Pool;

			if (pool.Count <= 5) return;

			int atStart = pool.Count;
			var itemsToKeep = new List<IEngine>();
			bool disposed = false;
			DateTime cutoffTime = DateTime.Now.AddSeconds(-60);


			while (pool.TryPop(out var item))
			{
				try
				{
					// Keep at least 5, and expire only if old enough
					if (itemsToKeep.Count >= 5 && item.LastAccess < cutoffTime)
					{
						disposed = true;
						item.Dispose();
						item.Container?.Dispose();
					}
					else
					{
						itemsToKeep.Add(item);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("!! - CleanupEngines - !!");
					Console.WriteLine(ex.ToString());
				}
			}

			// Push back in reverse order to maintain LIFO semantics
			// (most recently used should be on top)
			for (int i = itemsToKeep.Count - 1; i >= 0; i--)
			{
				pool.Push(itemsToKeep[i]);
			}

			if (disposed)
			{
				Process currentProcess = Process.GetCurrentProcess();
				long privateMemory = currentProcess.PrivateMemorySize64;
				
				Console.WriteLine($"Cleanup {rootEngine.Name} - Started with pool size:{atStart} - now:{itemsToKeep.Count} - Private Memory: {privateMemory / 1024 / 1024} MB");
			}
		}

		public void InitPerRequest(LightInject.IServiceContainer container, IEngine? engine = null)
		{
			engine ??= container.GetInstance<IEngine>();

			var msa = container.GetInstance<IMemoryStackAccessor>();
			var memoryStack = MemoryStack.New(container, engine);
			msa.Current = memoryStack;

			var context = new PLangContext(memoryStack, engine, ExecutionMode.Console);
			var ca = container.GetInstance<IPLangContextAccessor>();
			ca.Current = context;

		}


		private IEngine CreateEngine(string rootPath, IServiceContainer container)
		{
			var serviceContainer = new ServiceContainer();

			serviceContainer.RegisterForPLang(rootPath, "/",
								container.GetInstance<IErrorHandlerFactory>(), container.GetInstance<IErrorSystemHandlerFactory>(), rootEngine);


			var engine = serviceContainer.GetInstance<IEngine>();
			engine.Name = $"Child";

			InitPerRequest(serviceContainer);

			engine.Init(serviceContainer);
			engine.SetParentEngine(rootEngine);

			engine.SystemSink = rootEngine.SystemSink;
			engine.UserSink = rootEngine.UserSink;

			return engine;
		}
	}


	/*
	public record EngineInfo(IEngine Engine, DateTime LastAccess);


	public class EnginePool : IDisposable
	{
		private readonly ConcurrentQueue<EngineInfo> _pool = new();

		private readonly Func<IEngine> _factory;
		private readonly int _maxSize;
		private bool disposed;

		public EnginePool(int initialSize, Func<IEngine> factory, int maxSize = 50)
		{
			if (initialSize > maxSize)
				throw new ArgumentException("Initial size cannot exceed max size.");

			_factory = factory ?? throw new ArgumentNullException(nameof(factory));
			_maxSize = maxSize;

			_ = Task.Run(async () =>
			{
				while (!disposed)
				{
					await Task.Delay(TimeSpan.FromSeconds(30));
					await CheckPoolSize();
				}
			});

			for (int i = 0; i < initialSize; i++)
			{
				var engine = _factory();
				SetProperties(engine, engine.ParentEngine, null, engine.Name);

				_pool.Enqueue(new EngineInfo(engine, DateTime.Now));
			}
		}

		private async Task CheckPoolSize()
		{
			var now = DateTime.UtcNow;
			var keep = new List<EngineInfo>();

			while (_pool.TryDequeue(out var info))
			{
				if (now - info.LastAccess < TimeSpan.FromMinutes(3) || keep.Count < 2)
					keep.Add(info);
				else
				{
					var proc = Process.GetCurrentProcess();
					
					try { 
						info.Engine.Container.Dispose();
					}
					catch (Exception ex)
					{
						Console.WriteLine("Dispose engine on CheckPoolSize:" + ex);

					}
					
				}
			}

			foreach (var item in keep)
				_pool.Enqueue(item);


		}

		public void ReloadGoals()
		{
			foreach (var engineInfo in _pool)
			{
				engineInfo.Engine.PrParser.ForceLoadAllGoals();
				engineInfo.Engine.GetEventRuntime().Reload();
			}
		}

		public async Task<IEngine> RentAsync(IEngine parentEngine, GoalStep? callingStep, string name)
		{
			var proc = Process.GetCurrentProcess();

			if (_pool.TryDequeue(out var engineInfo))
			{
				return SetProperties(engineInfo.Engine, parentEngine, callingStep, name);
			}
			
			Console.WriteLine($"Create new engine: {_pool.Count}");

			var newEngine = _factory();
			return SetProperties(newEngine, parentEngine, callingStep, name);
		}

		private IEngine SetProperties(IEngine engine, IEngine parentEngine, GoalStep? callingStep, string name)
		{
			engine.Name = name;
			engine.SetParentEngine(parentEngine);			
			engine.AddContext("!plang.osPath", engine.FileSystem.SystemDirectory);
			engine.AddContext("!plang.rootPath", parentEngine.Path ?? engine.FileSystem.RootDirectory);
			engine.SystemSink = parentEngine.SystemSink;
			engine.UserSink = parentEngine.UserSink;

			foreach (var item in parentEngine.GetAppContext())
			{
				engine.GetAppContext().AddOrReplace(item.Key, item.Value);
			}


			return engine;
		}

		public void Return(IEngine engine, bool reset = false)
		{
			throw new Exception("Depricated - Return");
			engine.Return(reset);
			
			_pool.Enqueue(new EngineInfo(engine, DateTime.Now));
			Console.WriteLine("Return engine:" + _pool.Count);
		}

		public virtual void Dispose()
		{
			if (this.disposed)
			{
				return;
			}

			foreach (var item in _pool)
			{
				item.Engine.Dispose();
			}
			Console.WriteLine("Disposed engines in pool");
			this.disposed = true;

		}

		protected virtual void ThrowIfDisposed()
		{
			if (this.disposed)
			{
				throw new ObjectDisposedException(this.GetType().FullName);
			}
		}
	}*/
}
