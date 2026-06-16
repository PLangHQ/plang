using app;
using app.variable;
using Action = app.goal.steps.step.actions.action.@this;

namespace app.module.loop;

/// <summary>
/// Iterates over a collection, running the remaining actions in the step for each item.
/// Supports dictionaries (key/value), lists (index/value), and any IEnumerable.
/// Respects goal.return (Returned flag) and cancellation.
/// </summary>
[Action("foreach")]
public partial class Foreach : IContext, IStep
{
    public partial data.@this Collection { get; init; }
    public partial data.@this<app.variable.@this>? ItemName { get; init; }
    public partial data.@this<app.variable.@this>? KeyName { get; init; }

    public async Task<data.@this> Run()
    {
        if (await Collection.Value() == null)
            return global::app.data.@this.Ok(Result(itemCount: 0, completed: true));

        var variableName = (ItemName == null ? null : (await ItemName.Value())?.Name) ?? "item";
        int count = 0;

        // Find remaining actions in this step (the loop body)
        var bodyActions = GetBodyActions();

        // Data owns enumeration: dicts yield (dictKey, value), lists yield (index, element)
        foreach (var (key, item) in Collection.EnumerateItems())
        {
            if (Context.CancellationToken.IsCancellationRequested)
                return global::app.data.@this.Ok(Result(count, completed: false));

            await Context.Variable.Set(variableName, item);
            // Optional param: absent slots are non-null Uninitialized (null model), so
            // "was keyname supplied?" is IsInitialized, not a C# null check.
            if (KeyName is { IsInitialized: true })
                await Context.Variable.Set(await KeyName.Value(), key);

            foreach (var action in bodyActions)
            {
                var result = await action.RunAsync(Context);
                if (result.Returned) return result;
                if (!result.Success) return result;
            }
            count++;
        }

        var loopResult = global::app.data.@this.Ok(Result(count, completed: true));
        if (bodyActions.Count > 0)
            loopResult.Handled = true;
        return loopResult;
    }

    /// <summary>
    /// The foreach result as a native <c>dict</c> — <c>itemCount</c> (how many
    /// items ran) and <c>completed</c> (false when cancelled or returned early).
    /// A plain-data result, so it rides as a dict, not a dedicated type.
    /// </summary>
    private static global::app.type.dict.@this Result(int itemCount, bool completed)
        => new global::app.type.dict.@this()
            .Set("itemCount", (long)itemCount)
            .Set("completed", completed);

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
