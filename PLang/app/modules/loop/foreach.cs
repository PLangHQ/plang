using app;
using app.variables;
using Action = app.goals.goal.steps.step.actions.action.@this;

namespace app.modules.loop;

/// <summary>
/// Iterates over a collection, running the remaining actions in the step for each item.
/// Supports dictionaries (key/value), lists (index/value), and any IEnumerable.
/// Respects goal.return (Returned flag) and cancellation.
/// </summary>
[Action("foreach")]
public partial class Foreach : IContext, IStep
{
    public partial data.@this Collection { get; init; }
    public partial data.@this<Variable>? ItemName { get; init; }
    public partial data.@this<Variable>? KeyName { get; init; }

    public async Task<data.@this> Run()
    {
        if (Collection.Value == null)
            return global::app.data.@this.Ok(new types.loop { itemCount = 0, completed = true });

        var variableName = ItemName?.Value?.Name ?? "item";
        int count = 0;

        // Find remaining actions in this step (the loop body)
        var bodyActions = GetBodyActions();

        // Data owns enumeration: dicts yield (dictKey, value), lists yield (index, element)
        foreach (var (key, item) in Collection.EnumerateItems())
        {
            if (Context.CancellationToken.IsCancellationRequested)
                return global::app.data.@this.Ok(new types.loop { itemCount = count, completed = false });

            Context.Variables.Set(variableName, item);
            if (KeyName != null)
                Context.Variables.Set(KeyName.Value, key);

            foreach (var action in bodyActions)
            {
                var result = await action.RunAsync(Context);
                if (result.Returned) return result;
                if (!result.Success) return result;
            }
            count++;
        }

        var loopResult = global::app.data.@this.Ok(new types.loop { itemCount = count, completed = true });
        if (bodyActions.Count > 0)
            loopResult.Handled = true;
        return loopResult;
    }

    /// <summary>
    /// Gets the actions after this foreach in the same step — they form the loop body.
    /// </summary>
    private List<Action> GetBodyActions()
    {
        var actions = Step?.Actions;
        if (actions == null || __action == null) return new List<Action>();

        int myIndex = -1;
        for (int i = 0; i < actions.Count; i++)
        {
            if (ReferenceEquals(actions[i], __action))
            {
                myIndex = i;
                break;
            }
        }

        if (myIndex < 0 || myIndex + 1 >= actions.Count) return new List<Action>();

        return actions.Skip(myIndex + 1).ToList();
    }
}
