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
		public ConcurrentDictionary<string, byte> engineIds = new();
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

		public async Task<IEngine> RentAsync(GoalStep callingStep, int retryCount = 0)
		{
			var rootEngine = GetRootEngine();
			var enginePool = rootEngine.EnginePool;
			var pool = enginePool.Pool;

			if (pool.TryPop(out var engine))
			{
				engine.IsInPool = false;
				if (!enginePool.engineIds.TryRemove(engine.Id, out _))
				{
					if (retryCount < 5)
					{
						return await RentAsync(callingStep, ++retryCount);
					}
					throw new Exception($"Could not remove engineId ({engine.Id}) when renting engine. Retry count {retryCount}");
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

			if (enginePool.engineIds.ContainsKey(engine.Id))
			{
				Console.WriteLine($"RETURNING existing id: {engine.Id}");
				return;
			}

			engine.Reset(true);
			
			pool.Push(engine);
			enginePool.engineIds.TryAdd(engine.Id, 1);

			
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

}
