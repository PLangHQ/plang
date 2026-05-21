namespace app;

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
    Out,
    In
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
/// Marks properties that should be serialized when Data leaves the system (transport/wire outbound).
/// Used for envelope metadata like Signature.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class OutAttribute : Attribute { }

/// <summary>
/// Marks properties that should be deserialized when Data arrives from the wire (transport inbound).
/// Used for envelope metadata like Signature that needs to round-trip.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class InAttribute : Attribute { }

/// <summary>
/// Marks properties that contain sensitive data (e.g., private keys).
/// Excluded from all output serialization (global::app.channels.serializers.serializer.Json).
/// Included in storage serialization (raw JsonSerializer for DataSource).
/// Does NOT block code-level access — %MyIdentity.PrivateKey% still works.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveAttribute : Attribute { }
