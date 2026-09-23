using app.Attributes;

namespace app.module.action.code.defaultoverride;

/// <summary>
/// One captured default-selection override — the interface type whose default the user changed, and
/// the provider now chosen. Captured only when the current default differs from the built-in one,
/// so its presence IS the fact that a choice was made. A plang value, read back through its own
/// reader.
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    protected internal override global::app.type.@this Type => new("defaultoverride", typeof(@this));

    /// <summary>A structure, not a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>Self-write: the tagged fields, the View selecting the set.</summary>
    public override System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
        => OutputTagged(writer, mode, context);

    [Store, Out]
    public string TypeName { get; init; } = "";

    [Store, Out]
    public string ProviderName { get; init; } = "";

    public override string ToString() => $"{TypeName} → {ProviderName}";

    /// <summary>The pure core — an override passes through; nothing else converts into one.</summary>
    public static @this? Create(object? raw) => raw as @this;

    public static @this? Create(object? raw, global::app.actor.context.@this? context) => Create(raw);

    public static @this? Create(object? raw, global::app.data.@this data) => Create(raw);
}
