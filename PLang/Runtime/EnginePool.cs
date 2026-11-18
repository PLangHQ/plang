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

		// Counted on the root engine's pool, which every rent and return goes through.
		private int createdCount;
		private int disposedCount;
		/// <summary>Engines this pool has created and not disposed: the ones in the pool plus the ones rented out.</summary>
		public int Total => Volatile.Read(ref createdCount) - Volatile.Read(ref disposedCount);
		/// <summary>Engines waiting in the pool.</summary>
		public int Free => _pool.Count;
		/// <summary>Engines rented out and not returned yet.</summary>
		public int Rented => Math.Max(0, Total - Free);

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
				// The pop makes this engine ours: Return adds the id before it pushes, and only the Return that
				// adds the id pushes, so the pool never holds an engine twice and a popped engine has its id.
				enginePool.engineIds.TryRemove(engine.Id, out _);
				engine.IsInPool = false;

				try
				{
					InitPerRequest(rootEngine.Container, engine);
				}
				catch
				{
					// Not rented after all: back to the pool, so it is neither lost nor left registered.
					try { Return(engine); } catch { }
					throw;
				}
				return RegisterRented(rootEngine, engine, callingStep);
			}

			engine = CreateEngine(rootEngine.FileSystem.RootDirectory, rootEngine.Container);

			return RegisterRented(rootEngine, engine, callingStep);
		}

		// A rented engine is in the root engine's KeepAliveRegistry until Return, so `get running engines` lists it.
		private static IEngine RegisterRented(IEngine rootEngine, IEngine engine, GoalStep callingStep)
		{
			engine.RentedFrom = callingStep;
			rootEngine.KeepAliveRegistry.AddEngine(engine);
			return engine;
		}

		public void Return(IEngine engine, bool reset = false)
		{
			var rootEngine = GetRootEngine();
			var enginePool = rootEngine.EnginePool;
			var pool = enginePool.Pool;
			// First, so the entry goes even when the engine is already in the pool or Reset fails.
			rootEngine.KeepAliveRegistry.RemoveEngine(engine);
			if (engine.IsInPool)
			{
				return;
			}

			// Only one of two Returns of the same engine adds the id, and only that one pushes. The id goes in
			// before the push, so an engine that RentAsync pops always has its id.
			if (!enginePool.engineIds.TryAdd(engine.Id, 1))
			{
				Console.WriteLine($"RETURNING existing id: {engine.Id}");
				return;
			}
			engine.IsInPool = true;

			try
			{
				engine.Reset(true);
			}
			catch
			{
				// The engine cannot go back to the pool and nobody holds it: dispose it, so it is not left counted in Total.
				enginePool.engineIds.TryRemove(engine.Id, out _);
				Interlocked.Increment(ref enginePool.disposedCount);
				try
				{
					engine.Dispose();
					engine.Container?.Dispose();
				}
				catch { }
				throw;
			}

			pool.Push(engine);
		}


		/// <summary>
		/// Engines that CleanupEngines never disposes, and the number StartWarmUp creates after startup.
		/// A new engine costs 0.1 to 0.5 s of JIT and container setup, so a pool that shrinks to 5 after a
		/// minute of quiet makes every burst after the quiet pay again. Env PLANG_ENGINE_POOL_MIN, default 8,
		/// 0 turns both the floor (back to the old 5) and the warm-up off.
		/// </summary>
		public static readonly int MinPoolSize = ReadMinPoolSize();
		private static int ReadMinPoolSize()
		{
			var value = Environment.GetEnvironmentVariable("PLANG_ENGINE_POOL_MIN");
			if (int.TryParse(value, out int min) && min >= 0) return min == 0 ? 5 : min;
			return 8;
		}
		private static bool WarmUpDisabled => Environment.GetEnvironmentVariable("PLANG_ENGINE_POOL_MIN") == "0";

		private int warmUpStarted;

		/// <summary>
		/// Creates engines in the background until the pool holds MinPoolSize. One thread, one engine at a time,
		/// so serving is not delayed; call it once the app has started. Safe to call repeatedly.
		/// </summary>
		public void StartWarmUp()
		{
			if (WarmUpDisabled) return;
			if (Interlocked.Exchange(ref warmUpStarted, 1) == 1) return;

			Task.Run(() =>
			{
				try
				{
					var root = GetRootEngine();
					var pool = root.EnginePool.Pool;
					while (pool.Count < MinPoolSize)
					{
						var engine = CreateEngine(root.FileSystem.RootDirectory, root.Container);
						Return(engine);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("EnginePool warm-up stopped: " + ex.Message);
				}
			});
		}

		public void CleanupEngines()
		{
			var rootEngine = GetRootEngine();
			var pool = rootEngine.EnginePool.Pool;

			if (pool.Count <= MinPoolSize) return;

			int atStart = pool.Count;
			var itemsToKeep = new List<IEngine>();
			bool disposed = false;
			DateTime cutoffTime = DateTime.Now.AddSeconds(-60);


			while (pool.TryPop(out var item))
			{
				try
				{
					// Keep at least MinPoolSize, and expire only if old enough
					if (itemsToKeep.Count >= MinPoolSize && item.LastAccess < cutoffTime)
					{
						disposed = true;
						rootEngine.EnginePool.engineIds.TryRemove(item.Id, out _);
						Interlocked.Increment(ref rootEngine.EnginePool.disposedCount);
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
				GC.Collect(2, GCCollectionMode.Forced, true, true);
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

			Interlocked.Increment(ref GetRootEngine().EnginePool.createdCount);
			return engine;
		}
	}

}
