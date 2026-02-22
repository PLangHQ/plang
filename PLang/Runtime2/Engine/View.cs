namespace PLang.Runtime2.Engine;

/// <summary>
/// Which serialization view to use.
/// Each view includes only properties tagged with its attribute.
/// </summary>
public enum View
{
    Default,
    Store,
    LlmBuilder,
    Debug,
    Out
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class StoreAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public sealed class LlmBuilderAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public sealed class DebugAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public sealed class DefaultAttribute : Attribute { }

/// <summary>
/// Marks properties that should only be serialized when Data leaves the system (transport/wire view).
/// Used for envelope metadata like Signature and Properties with transport-specific data.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class OutAttribute : Attribute { }
