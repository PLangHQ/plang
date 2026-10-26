using app.Attributes;

namespace app.module.code.dll;

/// <summary>
/// A DLL a run took in through the code registry's one door (<see cref="code.@this.Load"/>), remembered by the door it
/// came through: <see cref="loaded.@this"/> (its providers and types, <c>load code</c>) or <see cref="added.@this"/> (its
/// modules, <c>add module</c>). A plang value written by its tagged members and read back by its own reader, so a
/// snapshot carries what came in, and starting it takes the DLL in again the same way.
/// </summary>
public abstract class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A DLL's record, born holding <paramref name="type"/> — the app's.</summary>
    private protected @this(global::app.type.@this type) : base(type) { }

    /// <summary>Plang keeps it for itself: no step names it.</summary>
    public static bool Internal => true;

    /// <summary>A structure, not a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>Where the DLL was taken in from.</summary>
    [Store, Out]
    public required global::app.type.item.path.@this Source { get; init; }

    /// <summary>Takes the DLL in again into <paramref name="code"/> through the door it first came through.</summary>
    internal abstract System.Threading.Tasks.Task<global::app.data.@this> Start(code.@this code, global::app.actor.context.@this context);

    public override string ToString() => Source.ToString() ?? "";

    /// <summary>The pure core — a record passes through; nothing else converts into one.</summary>
    public static @this? Create(object? raw) => raw as @this;

    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => Create(raw);
}
