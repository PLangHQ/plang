using app.Attributes;

namespace app.module.code.registration;

/// <summary>
/// One provider registered in-process — by the kind it was registered as and its name. A provider a DLL brought is the
/// DLL's (<see cref="dll.loaded.@this"/>), taken in again with it; one registered in-process has nothing to be made
/// again from, so a snapshot keeps its registration only to answer that it can't come back. A plang VALUE, written as a
/// typed value and read back through its own reader.
/// </summary>
[PlangType("code.registration")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A captured registration, born holding <paramref name="type"/> — the app's.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A structure, not a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>The kind of provider it was registered as, by its plang name (<c>signing</c>, <c>key</c>,
    /// <see cref="kind.@this"/>) — the identity that survives across processes and versions.</summary>
    [Store, Out]
    public string Kind { get; init; } = "";

    /// <summary>The provider, by the name it was registered under.</summary>
    [Store, Out]
    public string Provider { get; init; } = "";

    /// <summary>The pure core — a registration passes through; nothing else converts into one.</summary>
    public static @this? Create(object? raw) => raw as @this;

    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => Create(raw);
}
