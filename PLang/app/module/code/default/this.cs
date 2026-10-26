using app.Attributes;

namespace app.module.code.@default;

/// <summary>
/// The provider a kind's default is set to, where it differs from the one the kind was born with — so its presence IS
/// the fact that a choice was made. A plang value, read back through its own reader.
/// </summary>
[PlangType("code.default")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A kind's default, born holding <paramref name="type"/> — the app's.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A structure, not a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>The kind of provider, by its plang name (<c>signing</c>, <c>key</c>, <see cref="kind.@this"/>).</summary>
    [Store, Out]
    public string Kind { get; init; } = "";

    /// <summary>The provider the kind's default is set to, by its name.</summary>
    [Store, Out]
    public string Provider { get; init; } = "";

    /// <summary>The pure core — a default passes through; nothing else converts into one.</summary>
    public static @this? Create(object? raw) => raw as @this;

    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => Create(raw);
}
