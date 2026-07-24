using app;
using app.variable;
using Action = app.goal.step.action.@this;

namespace app.module.action.loop;

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
        // A value-less collection (the null citizen or an absent slot) iterates
        // zero times; an empty list/dict falls through and enumerates to zero
        // naturally. The null citizen Peeks itself (IsNull), absent Peeks null.
        var collectionValue = await Collection.Value();
        if (collectionValue == null || collectionValue.IsNull || collectionValue.Peek() == null)
            return Context.Ok(Result(Context, itemCount: 0, completed: true));

        var variableName = (ItemName == null ? null : (await ItemName.Value())?.Name) ?? "item";
        var keyVariableName = KeyName is { IsInitialized: true } ? (await KeyName.Value())?.Name : null;
        int count = 0;

        // Loop-in-a-loop: an inner loop reuses the same %item%/%key% names and would
        // leave them clobbered for the OUTER loop's body after it returns. Save the
        // outer bindings now and restore them when this loop exits — so a nested
        // `foreach` doesn't bleed its last item up into the enclosing loop.
        var savedItem = await Context.Variable.Get(variableName);
        var savedKey = keyVariableName != null ? await Context.Variable.Get(keyVariableName) : null;

        // The loop body — the actions after this foreach in the step's chain (v0.1 flat model).
        // Materialized once; the Handled flag below stops the outer chain from re-running them.
        var chain = Step?.Action;
        int myIndex = chain?.IndexOf(__action) ?? -1;
        var bodyActions = myIndex >= 0
            ? chain!.Elements.Skip(myIndex + 1).ToList()
            : new List<Action>();

        // Data owns enumeration: dicts yield (dictKey, value), lists yield (index, element)
        foreach (var (key, item) in await Collection.EnumerateItems())
        {
            if (Context.CancellationToken.IsCancellationRequested)
                return Context.Ok(Result(Context, count, completed: false));

            await Context.Variable.Set(variableName, item);
            // Optional param: absent slots are non-null Uninitialized (null model), so
            // "was keyname supplied?" is IsInitialized, not a C# null check.
            if (KeyName is { IsInitialized: true })
                await Context.Variable.Set(await KeyName.Value(), key);

            foreach (var action in bodyActions)
            {
                var result = await action.Run(Context);
                if (result.Returned) return result;
                if (!result.Success) return result;
            }
            count++;
        }

        // Restore the outer loop's bindings (see savedItem above) — a nested loop
        // must not leave its last item/key visible to the enclosing loop's body.
        if (savedItem.IsInitialized) await Context.Variable.Set(variableName, savedItem);
        if (keyVariableName != null && savedKey is { IsInitialized: true }) await Context.Variable.Set(keyVariableName, savedKey);

        var loopResult = Context.Ok(Result(Context, count, completed: true));
        if (bodyActions.Count > 0)
            loopResult.Handled = true;
        return loopResult;
    }

    /// <summary>
    /// The foreach result as a native <c>dict</c> — <c>itemCount</c> (how many
    /// items ran) and <c>completed</c> (false when cancelled or returned early).
    /// A plain-data result, so it rides as a dict, not a dedicated type.
    /// </summary>
    private static global::app.type.item.dict.@this Result(actor.context.@this context, int itemCount, bool completed)
        => new global::app.type.item.dict.@this(context)
            .Set("itemCount", (long)itemCount)
            .Set("completed", completed);

    /// <summary>
    /// Gets the actions after this foreach in the same step — they form the loop body.
    /// </summary>
    private global::app.goal.step.action.list.@this GetBodyActions()
    {
        var actions = Step?.Action;
        if (actions == null || __action == null) return new();

        int myIndex = actions.IndexOf(__action);
        if (myIndex < 0 || myIndex + 1 >= actions.Count) return new();

        var body = new global::app.goal.step.action.list.@this();
        foreach (var a in actions.Elements.Skip(myIndex + 1)) body.Add(a);
        return body;
    }
}
