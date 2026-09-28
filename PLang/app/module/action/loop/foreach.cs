using app;
using Action = app.goal.step.action.@this;

namespace app.module.action.loop;

/// <summary>
/// Iterates over a collection, running the remaining actions in the step for each item.
/// Supports dictionaries (key/value), lists (index/value), and any IEnumerable.
/// Respects goal.return (Returned flag) and cancellation.
/// </summary>
[Action("foreach")]
public partial class Foreach : IContext, IStep, IScope, ILoop
{
    /// <summary>At the build's walk: the item is bound to its collection's element — the empty value
    /// of the collection's kind — when the store knows the collection and its kind.</summary>
    public async Task Scope()
    {
        if (!Collection.IsVariable || await Collection.Follow(Context) is not { IsInitialized: true } known
            || known.Type?.kind is not { IsEmpty: false } kind || !Context.App.type.list.Contains(kind.Name)) return;
        var element = Context.App.type.list[kind.Name];
        var named = (Item == null ? null : await Item.Value()) ?? new app.type.item.variable.@this("item");
        await named.Set(new data.@this(named.Name, element.Empty(Context), element, context: Context), Context);
    }

    public partial data.@this Collection { get; init; }
    /// <summary>The variable each element is bound to — <c>%item%</c> when not named.</summary>
    [Default("item")]
    public partial data.@this<app.type.item.variable.@this>? Item { get; init; }
    /// <summary>The variable each key (dict key or list index) is bound to — unbound when not named.</summary>
    public partial data.@this<app.type.item.variable.@this>? Key { get; init; }

    public async Task<data.@this> Start()
    {
        // A value-less collection (the null citizen or an absent slot) iterates
        // zero times; an empty list/dict falls through and enumerates to zero
        // naturally. The null citizen Peeks itself (IsNull), absent Peeks null.
        var collectionValue = await Collection.Value();
        if (collectionValue == null || collectionValue.IsNull || collectionValue.Peek() == null)
            return Context.Ok(Result(itemCount: 0, completed: true));

        var itemVariable = (Item == null ? null : await Item.Value()) ?? new app.type.item.variable.@this("item");
        var keyVariable = Key is { IsInitialized: true } ? await Key.Value() : null;
        int count = 0;

        // Loop-in-a-loop: an inner loop reuses the same %item%/%key% names and would
        // leave them clobbered for the OUTER loop's body after it returns. Save the
        // outer bindings now and restore them when this loop exits — so a nested
        // `foreach` doesn't bleed its last item up into the enclosing loop.
        var savedItem = await itemVariable.Start(Context);
        var savedKey = keyVariable != null ? await keyVariable.Start(Context) : null;

        // The loop body — the actions after this foreach in the step's chain (v0.1 flat model).
        // Materialized once; the Handled flag below stops the outer chain from re-running them.
        var chain = Step?.Code;
        int myIndex = chain?.IndexOf(__action) ?? -1;
        var bodyActions = myIndex >= 0
            ? chain!.Items().Skip(myIndex + 1).ToList()
            : new List<Action>();

        // Data owns enumeration: dicts yield (dictKey, value), lists yield (index, element)
        foreach (var (key, item) in await Collection.EnumerateItems())
        {
            if (Context.CancellationToken.IsCancellationRequested)
                return Context.Ok(Result(count, completed: false));

            await itemVariable.Set(item, Context);
            // Optional param: absent slots are non-null Uninitialized (null model), so
            // "was a key named?" is IsInitialized, not a C# null check.
            if (keyVariable != null)
                await keyVariable.Set(key, Context);

            foreach (var action in bodyActions)
            {
                var result = await action.Start(Context);
                if (result.Returned) return result;
                if (!result.Success) return result;
            }
            count++;
        }

        // Restore the outer loop's bindings (see savedItem above) — a nested loop
        // must not leave its last item/key visible to the enclosing loop's body.
        if (savedItem.IsInitialized) await itemVariable.Set(savedItem, Context);
        if (keyVariable != null && savedKey is { IsInitialized: true }) await keyVariable.Set(savedKey, Context);

        var loopResult = Context.Ok(Result(count, completed: true));
        if (bodyActions.Count > 0)
            loopResult.Handled = true;
        return loopResult;
    }

    /// <summary>
    /// The foreach result as a native <c>dict</c> — <c>itemCount</c> (how many
    /// items ran) and <c>completed</c> (false when cancelled or returned early).
    /// A plain-data result, so it rides as a dict, not a dedicated type.
    /// </summary>
    private static global::app.type.item.dict.@this Result(int itemCount, bool completed)
        => new global::app.type.item.dict.@this()
            .Set("itemCount", (long)itemCount)
            .Set("completed", completed);

    /// <summary>
    /// Gets the actions after this foreach in the same step — they form the loop body.
    /// </summary>
    private global::app.goal.step.action.list.@this GetBodyActions()
    {
        var actions = Step?.Code;
        if (actions == null || __action == null) return new();

        int myIndex = actions.IndexOf(__action);
        if (myIndex < 0 || myIndex + 1 >= actions.Count) return new();

        var body = new global::app.goal.step.action.list.@this();
        foreach (var a in actions.Items().Skip(myIndex + 1)) body.Add(a);
        return body;
    }
}
