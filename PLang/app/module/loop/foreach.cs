using app;

namespace app.module.loop;

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
            return await Result(itemCount: 0, completed: true);

        var itemVariable = (Item == null ? null : await Item.Value()) ?? new app.type.item.variable.@this("item");
        var keyVariable = Key is { IsInitialized: true } ? await Key.Value() : null;
        int count = 0;

        // The loop body — the actions after this foreach in the step's chain. The Handled flag below stops
        // the outer chain from re-running them.
        var bodyActions = Step?.Code.After(__action!) ?? [];

        // Data owns enumeration: dicts yield (dictKey, value), lists yield (index, element). Each run of the
        // body is a call whose frame binds %item% (and %key%): the loop's own names end with it — an outer
        // loop's %item% is its own again after a nested one — while the body's other writes (a running
        // total) reach the caller.
        foreach (var (key, item) in await Collection.EnumerateItems())
        {
            if (Context.CancellationToken.IsCancellationRequested)
                return await Result(count, completed: false);

            var bound = new List<data.@this> { item.Copy(itemVariable.Name) };
            // Optional param: absent slots are non-null Uninitialized (null model), so
            // "was a key named?" is IsInitialized, not a C# null check.
            if (keyVariable != null) bound.Add(key.Copy(keyVariable.Name));

            await using (Context.Variable.Calls.Push(bound))
                foreach (var action in bodyActions)
                {
                    var result = await action.Start(Context);
                    if (result.Returned) return result;
                    if (!result.Success) return result;
                }
            count++;
        }

        var loopResult = await Result(count, completed: true);
        if (bodyActions.Count > 0)
            loopResult.Handled = true;
        return loopResult;
    }

    /// <summary>
    /// The foreach result, born as a native <c>dict</c> — <c>itemCount</c> (how many items ran) and
    /// <c>completed</c> (false when cancelled). A plain-data result, so it rides as a dict, not a dedicated type.
    /// </summary>
    private async Task<data.@this> Result(int itemCount, bool completed)
        => await Context.App.type.list["dict"].Create(
            new Dictionary<string, object?> { ["itemCount"] = (long)itemCount, ["completed"] = completed }, Context);
}
