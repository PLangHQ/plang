using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.actions;

namespace PLang.Tests.Runtime2.Core;

public class StepCacheTests
{
    private static Step MakeStepWithReturn(string module, string action,
        Dictionary<string, object?> parameters, string returnVarName,
        CacheSettings? cache = null, int index = 0, string text = "")
    {
        return new Step
        {
            Index = index,
            Text = text,
            Cache = cache,
            Actions = new StepActions
            {
                new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                {
                    Module = module,
                    ActionName = action,
                    Parameters = parameters.Select(kv => new Data(kv.Key, kv.Value)).ToList(),
                    Return = new List<Data> { new Data(returnVarName) }
                }
            }
        };
    }

    #region Cache Hit / Miss

    [Test]
    public async Task CachedStep_SecondRun_SkipsHandler()
    {
        await using var engine = new Engine("/app");

        var handler = new CountingHandler("first-result");
        engine.Libraries.Register("test", "fetch", handler);

        var step = MakeStepWithReturn("test", "fetch",
            new Dictionary<string, object?> { { "url", "https://example.com" } },
            returnVarName: "result",
            cache: new CacheSettings { DurationSeconds = 300 });
        step.Goal = new Goal { Name = "TestGoal", Path = "TestGoal" };

        using var context = engine.CreateContext();

        // First call: handler executes
        var result1 = await step.RunAsync(engine, context);
        await Assert.That(result1.Success).IsTrue();
        await Assert.That(handler.CallCount).IsEqualTo(1);
        await Assert.That(context.MemoryStack.GetValue("result")).IsEqualTo("first-result");

        // Second call: served from cache, handler NOT called again
        var result2 = await step.RunAsync(engine, context);
        await Assert.That(result2.Success).IsTrue();
        await Assert.That(handler.CallCount).IsEqualTo(1);
        await Assert.That(context.MemoryStack.GetValue("result")).IsEqualTo("first-result");
    }

    [Test]
    public async Task NoCacheSettings_HandlerAlwaysCalled()
    {
        await using var engine = new Engine("/app");

        var handler = new CountingHandler("value");
        engine.Libraries.Register("test", "fetch", handler);

        var step = MakeStepWithReturn("test", "fetch",
            new Dictionary<string, object?> { { "url", "https://example.com" } },
            returnVarName: "result",
            cache: null); // No caching

        using var context = engine.CreateContext();

        await step.RunAsync(engine, context);
        await step.RunAsync(engine, context);

        await Assert.That(handler.CallCount).IsEqualTo(2);
    }

    #endregion

    #region Variable Restoration

    [Test]
    public async Task CachedStep_RestoresVariableValue()
    {
        await using var engine = new Engine("/app");

        var handler = new CountingHandler("cached-value");
        engine.Libraries.Register("test", "fetch", handler);

        var step = MakeStepWithReturn("test", "fetch",
            new Dictionary<string, object?> { { "id", "123" } },
            returnVarName: "data",
            cache: new CacheSettings { DurationSeconds = 600 });
        step.Goal = new Goal { Name = "CacheGoal", Path = "CacheGoal" };

        using var context = engine.CreateContext();

        await step.RunAsync(engine, context);
        await Assert.That(context.MemoryStack.GetValue("data")).IsEqualTo("cached-value");

        // Clear the variable to prove cache restores it
        context.MemoryStack.Remove("data");
        await Assert.That(context.MemoryStack.GetValue("data")).IsNull();

        // Second run: cache restores the variable
        await step.RunAsync(engine, context);
        await Assert.That(context.MemoryStack.GetValue("data")).IsEqualTo("cached-value");
        await Assert.That(handler.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task CachedStep_RestoresTypeName()
    {
        await using var engine = new Engine("/app");

        var handler = new CountingHandler(42L); // long value
        engine.Libraries.Register("test", "compute", handler);

        var step = MakeStepWithReturn("test", "compute",
            new Dictionary<string, object?>(),
            returnVarName: "number",
            cache: new CacheSettings { DurationSeconds = 300 });
        step.Goal = new Goal { Name = "TypeGoal", Path = "TypeGoal" };

        using var context = engine.CreateContext();

        await step.RunAsync(engine, context);
        var data = context.MemoryStack.Get("number");
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Value).IsEqualTo(42L);

        // Clear and re-run from cache
        context.MemoryStack.Remove("number");
        await step.RunAsync(engine, context);

        var restored = context.MemoryStack.Get("number");
        await Assert.That(restored).IsNotNull();
        await Assert.That(restored!.Value).IsEqualTo(42L);
        await Assert.That(restored!.Type).IsNotNull();
        await Assert.That(restored!.Type!.Value).IsEqualTo("long");
    }

    #endregion

    #region Error Handling

    [Test]
    public async Task CachedStep_FailedAction_NotCached()
    {
        await using var engine = new Engine("/app");

        var handler = new FailOnFirstCallHandler();
        engine.Libraries.Register("test", "flaky", handler);

        var step = MakeStepWithReturn("test", "flaky",
            new Dictionary<string, object?>(),
            returnVarName: "result",
            cache: new CacheSettings { DurationSeconds = 300 });
        step.Goal = new Goal { Name = "ErrorGoal", Path = "ErrorGoal" };

        using var context = engine.CreateContext();

        // First call fails
        var result1 = await step.RunAsync(engine, context);
        await Assert.That(result1.Success).IsFalse();
        await Assert.That(handler.CallCount).IsEqualTo(1);

        // Second call: handler called again (failure was NOT cached)
        var result2 = await step.RunAsync(engine, context);
        await Assert.That(result2.Success).IsTrue();
        await Assert.That(handler.CallCount).IsEqualTo(2);
    }

    #endregion

    #region Cache Key

    [Test]
    public async Task CachedStep_DifferentSteps_DifferentCacheKeys()
    {
        await using var engine = new Engine("/app");

        var handler = new SequenceHandler("a", "b");
        engine.Libraries.Register("test", "fetch", handler);

        var step0 = MakeStepWithReturn("test", "fetch",
            new Dictionary<string, object?>(),
            returnVarName: "val",
            cache: new CacheSettings { DurationSeconds = 300 },
            index: 0);
        step0.Goal = new Goal { Name = "Goal", Path = "Goal" };

        var step1 = MakeStepWithReturn("test", "fetch",
            new Dictionary<string, object?>(),
            returnVarName: "val",
            cache: new CacheSettings { DurationSeconds = 300 },
            index: 1);
        step1.Goal = new Goal { Name = "Goal", Path = "Goal" };

        using var context = engine.CreateContext();

        await step0.RunAsync(engine, context);
        await Assert.That(context.MemoryStack.GetValue("val")).IsEqualTo("a");

        await step1.RunAsync(engine, context);
        await Assert.That(context.MemoryStack.GetValue("val")).IsEqualTo("b");

        await Assert.That(handler.CallCount).IsEqualTo(2);
    }

    [Test]
    public async Task CachedStep_CustomKey_WithVariableResolution()
    {
        await using var engine = new Engine("/app");

        var handler = new CountingHandler("result");
        engine.Libraries.Register("test", "fetch", handler);

        var step = MakeStepWithReturn("test", "fetch",
            new Dictionary<string, object?>(),
            returnVarName: "result",
            cache: new CacheSettings { DurationSeconds = 300, Key = "user:%userId%" });
        step.Goal = new Goal { Name = "Goal", Path = "Goal" };

        using var context = engine.CreateContext();
        context.MemoryStack.Set("userId", "42");

        // First call with userId=42
        await step.RunAsync(engine, context);
        await Assert.That(handler.CallCount).IsEqualTo(1);

        // Same userId: cache hit
        await step.RunAsync(engine, context);
        await Assert.That(handler.CallCount).IsEqualTo(1);

        // Different userId: cache miss
        context.MemoryStack.Set("userId", "99");
        await step.RunAsync(engine, context);
        await Assert.That(handler.CallCount).IsEqualTo(2);
    }

    #endregion

    #region ICache Interface

    [Test]
    public async Task MemoryStepCache_GetAsync_ReturnsNull_WhenNotSet()
    {
        var cache = new MemoryStepCache();

        var result = await cache.GetAsync("nonexistent");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task MemoryStepCache_SetAndGet_RoundTrips()
    {
        var cache = new MemoryStepCache();
        var entry = new StepCacheEntry
        {
            Variables = new Dictionary<string, CachedVariable>
            {
                ["x"] = new CachedVariable { Value = "hello", TypeName = "string" }
            }
        };

        await cache.SetAsync("key1", entry, new CacheSettings { DurationSeconds = 300 });
        var result = await cache.GetAsync("key1");

        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<StepCacheEntry>();
        var cached = (StepCacheEntry)result!;
        await Assert.That(cached.Variables.ContainsKey("x")).IsTrue();
        await Assert.That(cached.Variables["x"].Value).IsEqualTo("hello");
    }

    [Test]
    public async Task MemoryStepCache_RemoveAsync_DeletesEntry()
    {
        var cache = new MemoryStepCache();
        await cache.SetAsync("key1", "value", new CacheSettings { DurationSeconds = 300 });

        await cache.RemoveAsync("key1");
        var result = await cache.GetAsync("key1");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Engine_Cache_DefaultsToMemoryStepCache()
    {
        await using var engine = new Engine("/app");

        await Assert.That(engine.Cache).IsNotNull();
        await Assert.That(engine.Cache).IsTypeOf<MemoryStepCache>();
    }

    [Test]
    public async Task Engine_Cache_CanBeSwapped()
    {
        await using var engine = new Engine("/app");

        var custom = new FakeCache();
        engine.Cache = custom;

        await Assert.That(engine.Cache).IsEqualTo(custom);
    }

    [Test]
    public async Task CachedStep_UsesCustomCacheImplementation()
    {
        await using var engine = new Engine("/app");

        var fakeCache = new FakeCache();
        engine.Cache = fakeCache;

        var handler = new CountingHandler("value");
        engine.Libraries.Register("test", "fetch", handler);

        var step = MakeStepWithReturn("test", "fetch",
            new Dictionary<string, object?>(),
            returnVarName: "result",
            cache: new CacheSettings { DurationSeconds = 300 });
        step.Goal = new Goal { Name = "Goal", Path = "Goal" };

        using var context = engine.CreateContext();

        await step.RunAsync(engine, context);

        await Assert.That(fakeCache.GetCalls).IsEqualTo(1);
        await Assert.That(fakeCache.SetCalls).IsEqualTo(1);
    }

    #endregion

    #region StepCacheEntry

    [Test]
    public async Task StepCacheEntry_DefaultsToEmptyDictionary()
    {
        var entry = new StepCacheEntry();

        await Assert.That(entry.Variables).IsNotNull();
        await Assert.That(entry.Variables.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CachedVariable_StoresValueAndTypeName()
    {
        var cv = new CachedVariable { Value = 3.14, TypeName = "double" };

        await Assert.That(cv.Value).IsEqualTo(3.14);
        await Assert.That(cv.TypeName).IsEqualTo("double");
    }

    #endregion

    #region StepCache Property

    [Test]
    public async Task StepCache_IsNull_WhenNoCacheSettings()
    {
        var step = MakeStepWithReturn("test", "fetch",
            new Dictionary<string, object?>(), "result", cache: null);

        await Assert.That(step.StepCache).IsNull();
    }

    [Test]
    public async Task StepCache_IsNotNull_WhenCacheSettingsPresent()
    {
        var step = MakeStepWithReturn("test", "fetch",
            new Dictionary<string, object?>(), "result",
            cache: new CacheSettings { DurationSeconds = 300 });

        await Assert.That(step.StepCache).IsNotNull();
        await Assert.That(step.StepCache!.Settings.DurationSeconds).IsEqualTo(300);
    }

    [Test]
    public async Task StepCache_ReturnsSameInstance()
    {
        var step = MakeStepWithReturn("test", "fetch",
            new Dictionary<string, object?>(), "result",
            cache: new CacheSettings { DurationSeconds = 300 });

        var first = step.StepCache;
        var second = step.StepCache;

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }

    #endregion

    #region Cache Events

    [Test]
    public async Task CacheHitEvent_Fires_OnSecondCall()
    {
        await using var engine = new Engine("/app");

        var handler = new CountingHandler("value");
        engine.Libraries.Register("test", "fetch", handler);

        var step = MakeStepWithReturn("test", "fetch",
            new Dictionary<string, object?>(), "result",
            cache: new CacheSettings { DurationSeconds = 300 });
        step.Goal = new Goal { Name = "HitEventGoal", Path = "HitEventGoal" };

        int hitCount = 0;
        step.StepCache!.Hit.Add(new EventBinding(
            EventType.OnCacheHit,
            _ => { hitCount++; return Task.FromResult(Data.Ok()); }));

        using var context = engine.CreateContext();

        // First call: miss, no hit event
        await step.RunAsync(engine, context);
        await Assert.That(hitCount).IsEqualTo(0);

        // Second call: hit event fires
        await step.RunAsync(engine, context);
        await Assert.That(hitCount).IsEqualTo(1);
    }

    [Test]
    public async Task CacheMissEvent_Fires_OnFirstCall()
    {
        await using var engine = new Engine("/app");

        var handler = new CountingHandler("value");
        engine.Libraries.Register("test", "fetch", handler);

        var step = MakeStepWithReturn("test", "fetch",
            new Dictionary<string, object?>(), "result",
            cache: new CacheSettings { DurationSeconds = 300 });
        step.Goal = new Goal { Name = "MissEventGoal", Path = "MissEventGoal" };

        int missCount = 0;
        step.StepCache!.Miss.Add(new EventBinding(
            EventType.OnCacheMiss,
            _ => { missCount++; return Task.FromResult(Data.Ok()); }));

        using var context = engine.CreateContext();

        // First call: miss event fires
        await step.RunAsync(engine, context);
        await Assert.That(missCount).IsEqualTo(1);

        // Second call: cache hit, no miss event
        await step.RunAsync(engine, context);
        await Assert.That(missCount).IsEqualTo(1);
    }

    [Test]
    public async Task CacheEvents_BothFireCorrectly()
    {
        await using var engine = new Engine("/app");

        var handler = new CountingHandler("value");
        engine.Libraries.Register("test", "fetch", handler);

        var step = MakeStepWithReturn("test", "fetch",
            new Dictionary<string, object?>(), "result",
            cache: new CacheSettings { DurationSeconds = 300 });
        step.Goal = new Goal { Name = "BothEventsGoal", Path = "BothEventsGoal" };

        int hitCount = 0;
        int missCount = 0;
        step.StepCache!.Hit.Add(new EventBinding(
            EventType.OnCacheHit,
            _ => { hitCount++; return Task.FromResult(Data.Ok()); }));
        step.StepCache!.Miss.Add(new EventBinding(
            EventType.OnCacheMiss,
            _ => { missCount++; return Task.FromResult(Data.Ok()); }));

        using var context = engine.CreateContext();

        // Run 3 times: 1 miss + 2 hits
        await step.RunAsync(engine, context);
        await step.RunAsync(engine, context);
        await step.RunAsync(engine, context);

        await Assert.That(missCount).IsEqualTo(1);
        await Assert.That(hitCount).IsEqualTo(2);
    }

    #endregion

    #region Sliding Expiration

    [Test]
    public async Task MemoryStepCache_SlidingExpiration_SetsCorrectly()
    {
        var cache = new MemoryStepCache();
        var settings = new CacheSettings { DurationSeconds = 60, Sliding = true };

        await cache.SetAsync("sliding-key", "value", settings);
        var result = await cache.GetAsync("sliding-key");

        await Assert.That(result).IsEqualTo("value");
    }

    [Test]
    public async Task MemoryStepCache_AbsoluteExpiration_SetsCorrectly()
    {
        var cache = new MemoryStepCache();
        var settings = new CacheSettings { DurationSeconds = 60, Sliding = false };

        await cache.SetAsync("absolute-key", "value", settings);
        var result = await cache.GetAsync("absolute-key");

        await Assert.That(result).IsEqualTo("value");
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Handler that counts how many times it's called and returns a fixed value.
    /// </summary>
    private class CountingHandler : IClass, ICodeGenerated
    {
        private readonly object _returnValue;

        public CountingHandler(object returnValue) { _returnValue = returnValue; }

        public int CallCount { get; private set; }
        public Engine Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public void Initialize(Engine engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok(_returnValue));

        public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, Engine engine, PLangContext context)
        {
            Initialize(engine, context);
            CallCount++;
            return ExecuteAsync(null);
        }
    }

    /// <summary>
    /// Handler that fails on the first call and succeeds on subsequent calls.
    /// </summary>
    private class FailOnFirstCallHandler : IClass, ICodeGenerated
    {
        public int CallCount { get; private set; }
        public Engine Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public void Initialize(Engine engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());

        public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, Engine engine, PLangContext context)
        {
            Initialize(engine, context);
            CallCount++;
            if (CallCount == 1)
                return Task.FromResult(Data.FromError(new PLang.Runtime2.Engine.Errors.ActionError("flaky", "FlakyError", 500)));
            return Task.FromResult(Data.Ok("success"));
        }
    }

    /// <summary>
    /// Handler that returns different values on successive calls.
    /// </summary>
    private class SequenceHandler : IClass, ICodeGenerated
    {
        private readonly string[] _values;

        public SequenceHandler(params string[] values) { _values = values; }

        public int CallCount { get; private set; }
        public Engine Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public void Initialize(Engine engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());

        public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, Engine engine, PLangContext context)
        {
            Initialize(engine, context);
            var value = _values[Math.Min(CallCount, _values.Length - 1)];
            CallCount++;
            return Task.FromResult(Data.Ok(value));
        }
    }

    /// <summary>
    /// Fake ICache that tracks calls for verifying custom cache integration.
    /// </summary>
    private class FakeCache : ICache
    {
        private readonly Dictionary<string, object> _store = new();

        public int GetCalls { get; private set; }
        public int SetCalls { get; private set; }
        public int RemoveCalls { get; private set; }

        public Task<object?> GetAsync(string key, CancellationToken ct = default)
        {
            GetCalls++;
            _store.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task SetAsync(string key, object value, CacheSettings settings, CancellationToken ct = default)
        {
            SetCalls++;
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct = default)
        {
            RemoveCalls++;
            _store.Remove(key);
            return Task.CompletedTask;
        }
    }

    #endregion
}
