using System.Text.RegularExpressions;
using PLang.Runtime2.Context;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2;

/// <summary>
/// Behavioral wrapper around CacheSettings. Owns all cache logic:
/// key building, get/set, variable collection/restoration, and Hit/Miss events.
/// Per-request state (engine, context, ct) passed as parameters (OBP rule #4).
/// </summary>
public sealed class StepCache
{
    private readonly Step _step;

    public CacheSettings Settings { get; }
    public Bindings Hit { get; } = new();
    public Bindings Miss { get; } = new();

    public StepCache(Step step, CacheSettings settings)
    {
        _step = step;
        Settings = settings;
    }

    public async Task<Data> RunAsync(Engine engine, PLangContext context, CancellationToken ct)
    {
        var key = BuildCacheKey(context.MemoryStack);
        var cached = await engine.Cache.GetAsync(key, ct);

        if (cached is StepCacheEntry entry)
        {
            RestoreVariables(entry, context.MemoryStack);
            await Hit.Run(context);
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
        {
            return Regex.Replace(Settings.Key, @"%([^%]+)%", match =>
            {
                var varName = match.Groups[1].Value;
                var value = memoryStack.GetValue(varName);
                return value?.ToString() ?? match.Value;
            });
        }

        var goalPath = _step.Goal?.Path ?? "unknown";
        return $"step:{goalPath}:{_step.Index}";
    }

    private StepCacheEntry CollectReturnVariables(MemoryStack memoryStack)
    {
        var entry = new StepCacheEntry();
        foreach (var action in _step.Actions)
        {
            if (action.Return == null) continue;
            foreach (var returnVar in action.Return)
            {
                var data = memoryStack.Get(returnVar.Name);
                if (data != null)
                {
                    entry.Variables[returnVar.Name] = new CachedVariable
                    {
                        Value = data.Value,
                        TypeName = data.Type?.Value
                    };
                }
            }
        }
        return entry;
    }

    private static void RestoreVariables(StepCacheEntry entry, MemoryStack memoryStack)
    {
        foreach (var (name, cached) in entry.Variables)
        {
            var type = cached.TypeName != null ? new Memory.Type(cached.TypeName) : null;
            memoryStack.Set(name, cached.Value, type);
        }
    }
}
