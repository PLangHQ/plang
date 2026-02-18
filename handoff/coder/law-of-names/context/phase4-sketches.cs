// === Phase 4 Reference Code ===
// These are sketches — adapt to match actual code patterns.

// --- EngineCache (Engine/Cache/this.cs) ---
// Convention-wired wrapper around ICache.
// engine.Cache.GetAsync(...) delegates to the pluggable implementation.

namespace PLang.Runtime2.Engine.Cache;

public sealed class EngineCache
{
    private ICache _implementation;

    public EngineCache()
    {
        _implementation = new MemoryStepCache();
    }

    /// <summary>
    /// The backing cache implementation. Default: MemoryStepCache.
    /// Swap via: - use 'redis.dll' for caching
    /// </summary>
    public ICache Implementation
    {
        get => _implementation;
        set => _implementation = value ?? throw new ArgumentNullException(nameof(value));
    }

    public Task<object?> GetAsync(string key, CancellationToken ct = default)
        => _implementation.GetAsync(key, ct);

    public Task SetAsync(string key, object value, CacheSettings settings, CancellationToken ct = default)
        => _implementation.SetAsync(key, value, settings, ct);

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => _implementation.RemoveAsync(key, ct);
}


// --- EngineDebug (Engine/Debug/this.cs) ---
// Converted from static to instance. Engine owns it.
// Currently: DebugMode.Apply(engine, value)
// After:     engine.Debug.Enable(value)

namespace PLang.Runtime2.Engine.Debug;

public sealed class EngineDebug
{
    private readonly Engine _engine;

    public EngineDebug(Engine engine)
    {
        _engine = engine;
    }

    public bool IsEnabled => _engine.IsDebugMode;

    /// <summary>
    /// Enables debug mode. Registers before/after step events that dump
    /// step info, call stack, and memory stack to stderr.
    /// </summary>
    public void Enable(object debugValue)
    {
        _engine.IsDebugMode = true;
        // ... (move body of current DebugMode.Apply here,
        //      replacing 'engine' parameter with '_engine')
    }
}


// --- EngineTesting (Engine/Testing/this.cs) ---
// Converted from static to instance. Engine owns it.
// Currently: TestMode.RunAsync(engine, ct)
// After:     engine.Testing.RunAsync(ct)

namespace PLang.Runtime2.Engine.Testing;

public sealed class EngineTesting
{
    private readonly Engine _engine;

    public EngineTesting(Engine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Discovers and runs all *.test.goal files, prints summary, returns exit code.
    /// Each test file gets a fresh engine for full isolation.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        // ... (move body of current TestMode.RunAsync here,
        //      replacing 'engine' parameter with '_engine')
    }
}


// --- Engine updates ---
// In Engine constructor, add:
//   Cache = new EngineCache();
//   Debug = new EngineDebug(this);
//   Testing = new EngineTesting(this);
//
// Replace properties:
//   ICache Cache { get; set; } = new MemoryStepCache();
//   → EngineCache Cache { get; }
//
// Add new properties:
//   EngineDebug Debug { get; }
//   EngineTesting Testing { get; }
//
// Update callers:
//   DebugMode.Apply(engine, value)  → engine.Debug.Enable(value)
//   TestMode.RunAsync(engine, ct)   → engine.Testing.RunAsync(ct)
//   engine.Cache (was ICache)       → engine.Cache (now EngineCache, same API surface)
//   StepCache uses engine.Cache.GetAsync — still works (EngineCache delegates)
