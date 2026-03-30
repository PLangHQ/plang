using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Goals.Goal.Steps.Step;

/// <summary>
/// Behavioral wrapper around CacheSettings. Owns all cache logic:
/// key building, get/set, variable collection/restoration, and Hit/Miss events.
/// Per-request state (engine, context, ct) passed as parameters (OBP rule #4).
/// </summary>
public sealed class StepCache
{
    private readonly @this _step;

    public CacheSettings Settings { get; }
    public Bindings Hit { get; } = new();
    public Bindings Miss { get; } = new();

    public StepCache(@this step, CacheSettings settings)
    {
        _step = step;
        Settings = settings;
    }

    public async Task<Data> RunAsync(Engine.@this engine, PLangContext context, CancellationToken ct)
    {
        var key = BuildCacheKey(context.MemoryStack);
        var cached = await engine.Cache.GetAsync(key, ct);

        if (cached != null)
        {
            RestoreVariables(cached, context.MemoryStack);
            await Hit.Run(context);
            // Return the first restored variable's Data so the action return mapping
            // gets the correct value (not a null-valued cache entry)
            if (cached.Properties.Count > 0)
                return cached.Properties[0];
            return Data.Ok();
        }

        await Miss.Run(context);
        var result = await _step.Actions.RunAsync(engine, context, ct);
        if (!result.Success) return result;

        var cacheEntry = CollectReturnVariables(context.MemoryStack);
        await engine.Cache.SetAsync(key, cacheEntry, Settings, ct);
        return result;
    }

    private string BuildCacheKey(MemoryStack memoryStack)
    {
        if (!string.IsNullOrEmpty(Settings.Key))
            return memoryStack.Resolve(Settings.Key);

        var goalPath = _step.Goal?.Path ?? "unknown";
        return $"step:{goalPath}:{_step.Index}";
    }

    private Data CollectReturnVariables(MemoryStack memoryStack)
    {
        var entry = Data.Ok();
        foreach (var action in _step.Actions)
        {
            if (action.Return == null) continue;
            foreach (var returnVar in action.Return)
            {
                var data = memoryStack.Get(returnVar.Name);
                if (data != null)
                {
                    entry.Properties[returnVar.Name] = data;
                }
            }
        }
        return entry;
    }

    private static void RestoreVariables(Data cached, MemoryStack memoryStack)
    {
        foreach (var data in cached.Properties)
        {
            memoryStack.Set(data.Name, data.Value, data.Type);
        }
    }
}
