using app.Attributes;

namespace app.module.action.code.registration;

/// <summary>
/// One captured provider registration — the interface it was registered against, the provider
/// instance's name, and the DLL it came from. A plang VALUE, not a CLR record: the snapshot writes
/// it as a typed value and reads it back through its own reader, so it crosses the wire the way
/// every other value does rather than being reflected into and out of existence.
/// <para><c>Source</c> is null for an in-process registration on the same App — a registration the
/// fresh side cannot reproduce, which Restore rejects rather than guesses at.</para>
/// </summary>
[PlangType("registration")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    protected internal override global::app.type.@this Type => new("registration", typeof(@this));

    /// <summary>A structure, not a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>Self-write: the tagged fields, the View selecting the set.</summary>
    public override System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
        => OutputTagged(writer, mode, context);

    /// <summary>AssemblyQualifiedName of the provider interface — the identity that has to survive
    /// across processes.</summary>
    [Store, Out]
    public string TypeName { get; init; } = "";

    [Store, Out]
    public string ProviderName { get; init; } = "";

    [Store, Out]
    public string? Source { get; init; }

    public override string ToString() => $"{ProviderName} ({TypeName})";

    /// <summary>The pure core — a registration passes through; nothing else converts into one.</summary>
    public static @this? Create(object? raw) => raw as @this;

    public static @this? Create(object? raw, global::app.actor.context.@this? context) => Create(raw);

    public static @this? Create(object? raw, global::app.data.@this data) => Create(raw);
}
