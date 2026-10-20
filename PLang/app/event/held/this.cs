namespace app.@event.held;

/// <summary>
/// What a context binds on a shared program object — a goal, a step, an action, read once and run by every app made
/// from the same code. A binding a program makes while it runs (<c>on.event</c>) is its own app's, so it is held here,
/// on the binding actor's context (the system's, for an app-wide one), never on the object every app shares: a fresh
/// app's contexts hold none. The object's events reach it as its innermost level (<see cref="Level"/>); what is bound
/// on the object itself (a program's clauses, attached when it is read) stays the program's.
/// </summary>
internal sealed class @this : global::app.type.item.@this
{
    /// <summary>plang's own plumbing, never named by a program as a type.</summary>
    public static bool Internal => true;

    private @this(global::app.type.item.@this shared) : base(shared.Type) => EventsOf(shared.GetType());

    // The holds a context keeps, by the object held: made at the first binding, gone with the context.
    internal sealed class list
    {
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<global::app.type.item.@this, @this> _held = new();

        internal @this? this[global::app.type.item.@this shared] => _held.TryGetValue(shared, out var held) ? held : null;

        internal @this Hold(global::app.type.item.@this shared) => _held.GetValue(shared, static s => new @this(s));
    }

    /// <summary>The hold <paramref name="context"/>'s app keeps on <paramref name="shared"/> at <paramref name="at"/> —
    /// the asking actor's first, then the system's (an app-wide binding's); null past the last held.</summary>
    internal static global::app.type.item.@this? Level(global::app.type.item.@this shared, int at, global::app.actor.context.@this context)
    {
        var mine = context.Actor?.Context.Held?[shared];
        var system = context.App.actor.list.System.Context;
        var app = ReferenceEquals(system, context.Actor?.Context) ? null : system.Held?[shared];
        return at switch
        {
            0 => mine ?? app,
            1 => mine != null ? app : null,
            _ => null,
        };
    }
}
