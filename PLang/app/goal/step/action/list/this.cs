using app.data;
using Action = app.goal.step.action.@this;
using System.Collections.Generic;

namespace app.goal.step.action.list;

/// <summary>
/// The action NODE — a step's actions (`step.Action`). Typed storage that owns the chain resolution
/// (Rule 5). Twin of <see cref="app.goal.step.list.@this"/>, plus <see cref="IndexOf"/> for the coverage
/// key. Its <see cref="Run"/> IS the fire-or-fall-through: each action runs; a fired condition runs its
/// own <c>Child</c> and stops the chain — no cross-object sibling reach, no <c>Handled</c> signal for the
/// branch (`Handled` stays only as the event-handled stop).
/// </summary>
public sealed class @this
{
    // See step.list — a List reused when the caller has one, wrapped once otherwise; Add is a
    // construction affordance only (the graph is read-only after load).
    private readonly List<Action> _actions;
    public @this(IReadOnlyList<Action> actions) => _actions = actions as List<Action> ?? new List<Action>(actions);

    public Action this[int i] => _actions[i];                  // step.action[0]
    public IReadOnlyList<Action> list => _actions;            // step.action.list
    public int Count => _actions.Count;
    public void Add(Action action) => _actions.Add(action);   // construction only
    public int IndexOf(Action a) => _actions.IndexOf(a);      // coverage key

    /// <summary>Runs the chain: setup / non-condition actions dispatch in order; a condition evaluates,
    /// and if truthy runs its <c>Child</c> branch and stops (the rest of the chain — elseif/else — is
    /// skipped). An ordinary action has an empty Child so never enters.</summary>
    public async System.Threading.Tasks.Task<data.@this> Run(actor.context.@this context)
    {
        data.@this result = context.Ok();
        foreach (var action in _actions)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            result = await action.Run(context);
            if (result.ShouldExit() || result.Handled) break;         // return/exit, or a legit event-handled stop
            if (action.IsCondition && await result.ToBooleanAsync())
            {
                result = await action.Child.Run(context);             // gate fired → run the branch body
                break;                                                // branch taken → skip the rest of the chain
            }
        }
        return result;
    }
}
