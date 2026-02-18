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
    Debug
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class StoreAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public sealed class LlmBuilderAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public sealed class DebugAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public sealed class DefaultAttribute : Attribute { }
