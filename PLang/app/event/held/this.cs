namespace app.@event.held;

/// <summary>
/// What an app binds on something every app made from the same code shares — a program's goal, step or action (read
/// once), a type entry of the shared type list. A binding made while the app runs is its own, so it is held here, on
/// the binding actor's context (the system's, for an app-wide one), never on the shared object: a fresh app's contexts
/// hold none, and a binding goes with its app. An event, at each of its item's levels, starts what the level's object
/// holds and what the asker's app holds for it (<see cref="On"/>); what is bound on the object itself (a program's
/// clauses, attached when it is read) stays the program's.
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

    /// <summary>Whether <paramref name="item"/> is shared by every app made from the same code — what an app binds on it
    /// is held, not put on it: a program's goal, step or action (a module's catalog action is the app's own), a type
    /// entry of the shared list.</summary>
    internal static bool IsShared(global::app.type.item.@this item) => item switch
    {
        global::app.goal.step.action.@this action => !ReferenceEquals(action.Module[action.Name], action),
        global::app.goal.@this or global::app.goal.step.@this => true,
        global::app.type.@this type => type.Types?.IsShared == true,
        _ => false,
    };

    /// <summary>What <paramref name="item"/>'s events are as <paramref name="context"/>'s app sees them, at
    /// <paramref name="at"/>: its own first, then what the asking actor's context holds for it, then the system's;
    /// null past the last.</summary>
    internal static global::app.@event.on.@this? On(global::app.type.item.@this item, int at, global::app.actor.context.@this context)
    {
        if (at == 0) return item.on;
        var mine = context.Actor?.Context.Held?[item];
        var system = context.App.actor.list.System.Context;
        var app = ReferenceEquals(system, context.Actor?.Context) ? null : system.Held?[item];
        return at switch
        {
            1 => (mine ?? app)?.on,
            2 => mine != null ? app?.on : null,
            _ => null,
        };
    }

    /// <summary>The events a binding on <paramref name="item"/> is added to, made by <paramref name="context"/>'s app for
    /// <paramref name="scope"/>: a shared object's are held on the binding actor's context (the system's for an
    /// app-wide binding); any other item's are its own.</summary>
    internal static global::app.@event.on.own Own(global::app.type.item.@this item, global::app.actor.context.@this context,
        global::app.@event.binding.Scope scope)
    {
        if (!IsShared(item)) return item.Own();
        var keeper = scope == global::app.@event.binding.Scope.app ? context.App.actor.list.System.Context : context.Actor!.Context;
        return keeper.Holding.Hold(item).Own();
    }
}
