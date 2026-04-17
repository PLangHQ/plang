using App;
using App.Variables;
using Action = App.Goals.Goal.Steps.Step.Actions.Action.@this;

namespace App.modules.loop;

/// <summary>
/// Iterates over a collection, running the remaining actions in the step for each item.
/// Supports dictionaries (key/value), lists (index/value), and any IEnumerable.
/// Respects goal.return (Returned flag) and cancellation.
/// </summary>
[Example("foreach %items%, call ProcessItem item=%item%", "Collection=%items%, ItemName=item (+ goal.call action)")]
[Example("foreach %files%, read file %file%, write to %content%", "Collection=%files%, ItemName=file (+ file.read + variable.set actions)")]
[Example("foreach %users%, write out %user.name%", "Collection=%users%, ItemName=user (+ output.write action)")]
[Action("foreach")]
public partial class Foreach : IContext, IStep
{
    public partial Data.@this Collection { get; init; }
    [VariableName]
    public partial string? ItemName { get; init; }
    [VariableName]
    public partial string? KeyName { get; init; }

    public async Task<Data.@this> Run()
    {
        if (Collection.Value == null)
            return Data(new types.loop { itemCount = 0, completed = true });

        var variableName = ItemName ?? "item";
        int count = 0;

        // Find remaining actions in this step (the loop body)
        var bodyActions = GetBodyActions();

        // Data owns enumeration: dicts yield (dictKey, value), lists yield (index, element)
        foreach (var (key, item) in Collection.EnumerateItems())
        {
            if (Context.CancellationToken.IsCancellationRequested)
                return Data(new types.loop { itemCount = count, completed = false });

            item.Name = variableName;
            Context.Variables.Put(item);
            if (KeyName != null)
            {
                key.Name = KeyName;
                Context.Variables.Put(key);
            }

            foreach (var action in bodyActions)
            {
                var result = await action.RunAsync(Context);
                if (result.Returned) return result;
                if (!result.Success && !result.Handled) return result;
            }
            count++;
        }

        var loopResult = Data(new types.loop { itemCount = count, completed = true });
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
