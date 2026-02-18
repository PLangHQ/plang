using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Cache;

/// <summary>
/// Cached snapshot of a step's return variables.
/// For in-memory: Value is a live object reference.
/// For Redis/external: the ICache impl serializes via engine.Serializers.
/// </summary>
public sealed class StepCacheEntry
{
    public Dictionary<string, CachedVariable> Variables { get; init; } = new();
}

/// <summary>
/// A single cached variable: value + PLang type name for restoring Memory.Type.
/// </summary>
public sealed class CachedVariable
{
    public object? Value { get; init; }
    public string? TypeName { get; init; }
}
