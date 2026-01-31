using LightInject;
using PLang.Container;
using PLang.Interfaces;
using System.Collections.Concurrent;

namespace PLang.Runtime;

/// <summary>
/// Manages a pool of engines for concurrent request handling.
/// Uses a static pool shared across all container instances.
/// Creates engines on demand - first Rent() creates the first engine.
/// </summary>
public class EnginePoolService : IEnginePool
{
	// Static pool shared across all instances
	private static readonly ConcurrentStack<IEngine> _pool = new();
	private static readonly object _cleanupLock = new();
	private static int _totalCreated;
	private static Timer? _cleanupTimer;
	private static bool _cleanupTimerInitialized;

	private readonly IPLangFileSystem _fileSystem;
	private bool _disposed;

	// Configuration
	private const int MinPoolSize = 5;
	private const int MaxPoolSize = 50;
	private const int CleanupIntervalSeconds = 60;
	private const int EngineIdleTimeoutSeconds = 120;

	public EnginePoolService(IPLangFileSystem fileSystem)
	{
		_fileSystem = fileSystem;

		// Initialize cleanup timer once (thread-safe)
		if (!_cleanupTimerInitialized)
		{
			lock (_cleanupLock)
			{
				if (!_cleanupTimerInitialized)
				{
					_cleanupTimer = new Timer(
						_ => CleanupIdleEngines(),
						null,
						TimeSpan.FromSeconds(CleanupIntervalSeconds),
						TimeSpan.FromSeconds(CleanupIntervalSeconds));
					_cleanupTimerInitialized = true;
				}
			}
		}
	}

	public int AvailableCount => _pool.Count;
	public int TotalCreated => _totalCreated;

	/// <summary>
	/// Pre-warm the pool by creating engines in the background.
	/// Call this after the main engine is initialized.
	/// </summary>
	public void PreWarm(IEngine parentEngine, int count = 5)
	{
		Task.Run(() =>
		{
			for (int i = 0; i < count && _pool.Count < MaxPoolSize; i++)
			{
				var engine = CreateEngine(parentEngine);
				_pool.Push(engine);
			}
		});
	}

	public IEngine Rent(IEngine? parentEngine = null)
	{
		if (_pool.TryPop(out var engine))
		{
			PrepareForRequest(engine);
			return engine;
		}

		return CreateEngine(parentEngine);
	}

	public void Return(IEngine engine)
	{
		if (engine == null) return;

		try
		{
			// Reset engine state
			engine.Reset();
			engine.LastAccess = DateTime.UtcNow;

			// If we're over max, just dispose it
			if (_pool.Count >= MaxPoolSize)
			{
				DisposeEngine(engine);
				return;
			}

			_pool.Push(engine);
		}
		catch
		{
			// Silently handle errors during return
		}
	}

	private IEngine CreateEngine(IEngine? parentEngine = null)
	{
		var container = new ServiceContainer();

		if (parentEngine != null)
		{
			// Fast path: use parent engine's services (reuses PrParser, output sinks, etc.)
			container.RegisterForPLang(_fileSystem.RootDirectory, "/", parentEngine);
		}
		else
		{
			// Slow path: full standalone registration (fallback)
			container.RegisterForPLangConsole(_fileSystem.RootDirectory, "/");
		}

		var engine = container.GetInstance<IEngine>();
		engine.Name = $"PooledEngine-{Interlocked.Increment(ref _totalCreated)}";

		// Initialize engine
		engine.Init(container);

		// Prepare fresh context
		PrepareForRequest(engine);

		return engine;
	}

	private void PrepareForRequest(IEngine engine)
	{
		var container = engine.Container;

		// Create fresh memory stack
		var msa = container.GetInstance<IMemoryStackAccessor>();
		var memoryStack = MemoryStack.New(container, engine);
		msa.Current = memoryStack;

		// Create fresh context (clones module registry via copy-on-write)
		var context = new PLangContext(memoryStack, engine, ExecutionMode.HttpRequest);
		var contextAccessor = container.GetInstance<IPLangContextAccessor>();
		contextAccessor.Current = context;

		engine.LastAccess = DateTime.UtcNow;
	}

	private static void CleanupIdleEngines()
	{
		// Don't block if cleanup is already running
		if (!Monitor.TryEnter(_cleanupLock)) return;

		try
		{
			if (_pool.Count <= MinPoolSize) return;

			var cutoffTime = DateTime.UtcNow.AddSeconds(-EngineIdleTimeoutSeconds);
			var toKeep = new List<IEngine>();

			while (_pool.TryPop(out var engine))
			{
				if (toKeep.Count < MinPoolSize || engine.LastAccess >= cutoffTime)
				{
					toKeep.Add(engine);
				}
				else
				{
					DisposeEngine(engine);
				}
			}

			// Push back (reverse to maintain LIFO)
			for (int i = toKeep.Count - 1; i >= 0; i--)
			{
				_pool.Push(toKeep[i]);
			}
		}
		finally
		{
			Monitor.Exit(_cleanupLock);
		}
	}

	private static void DisposeEngine(IEngine engine)
	{
		try
		{
			engine.Dispose();
			(engine.Container as IDisposable)?.Dispose();
		}
		catch
		{
			// Silently handle disposal errors
		}
	}

	public void Dispose()
	{
		// Instance disposal - don't dispose the shared static pool
		// The static pool persists across container instances
		_disposed = true;
	}

	/// <summary>
	/// Disposes all pooled engines and stops the cleanup timer.
	/// Call this only when the application is shutting down.
	/// </summary>
	public static void DisposeAll()
	{
		_cleanupTimer?.Dispose();
		_cleanupTimer = null;
		_cleanupTimerInitialized = false;

		while (_pool.TryPop(out var engine))
		{
			try
			{
				engine.Dispose();
				(engine.Container as IDisposable)?.Dispose();
			}
			catch { }
		}
	}
}
