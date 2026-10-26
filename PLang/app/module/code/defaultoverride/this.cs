using app.Attributes;

namespace app.module.code.defaultoverride;

/// <summary>
/// One captured default-selection override — the kind of provider whose default the user changed, and
/// the provider now chosen. Captured only when the current default differs from the built-in one,
/// so its presence IS the fact that a choice was made. A plang value, read back through its own
/// reader.
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A captured override, born holding <paramref name="type"/> — the app's.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A structure, not a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>The kind of provider, by its plang name (<c>signing</c>, <c>key</c>, <see cref="kind.@this"/>).</summary>
    [Store, Out]
    public string Kind { get; init; } = "";

    [Store, Out]
    public string ProviderName { get; init; } = "";

    public override string ToString() => $"{Kind} → {ProviderName}";

    /// <summary>The pure core — an override passes through; nothing else converts into one.</summary>
    public static @this? Create(object? raw) => raw as @this;

    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => Create(raw);
}
