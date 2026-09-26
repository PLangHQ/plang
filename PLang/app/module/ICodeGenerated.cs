using app.actor.context;
using app.error;
using ActionType = app.goal.step.action.@this;

namespace app.module;

/// <summary>
/// Implemented by generated partial handler classes. A handler instance is the
/// per-execution home, built fresh for each action run.
///
///   Resolve — validate + decode the .pr parameters and construct a fresh, fully
///     populated handler instance (params bound via the object initializer). Called on
///     a throwaway registry-created shell; returns the real ready instance (or an error).
///   Attach  — set the markers on THIS instance (Context / Action / Step / Static / [Code]
///     provider, the app's from its start). Called by Resolve, and directly on prebound (inline
///     C#-composed) handlers whose params are already set. The build binds through it, so it looks
///     up nothing a step registers while the app runs.
///   Execute — find what exists only at run (the Channel; a missing one fails the run), then run
///     the handler's typed Run(), wrapping bare exceptions with the action's
///     module.action context.
///
/// All handlers must implement this interface — App requires it (no fallback path).
/// </summary>
public interface ICodeGenerated
{
    // Generated handlers override Resolve/Attach with the full param-resolution + marker
    // wiring. The defaults here serve hand-written handlers (test doubles) that carry no
    // .pr parameters: Resolve just Attaches and hands back this instance.
    async Task<(ICodeGenerated? Handler, global::app.error.Error? Error)> Resolve(ActionType action, actor.context.@this context)
    {
        var err = await Attach(action, context);
        return err != null ? (null, err) : (this, null);
    }

    Task<global::app.error.Error?> Attach(ActionType? action, actor.context.@this context) => Task.FromResult<global::app.error.Error?>(null);

    Task<data.@this> Execute();

    /// <summary>
    /// Per-property snapshot of pr-side and final-resolved values.
    /// Called from the call-stack catch block so the resulting Error carries
    /// enough context to diagnose "param X arrived as Y" without re-running.
    /// Default (no parameter properties) returns an empty list.
    /// </summary>
    List<ParamSnapshot> SnapshotParams() => new();
}
