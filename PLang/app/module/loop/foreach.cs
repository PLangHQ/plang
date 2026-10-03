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
        if (await Item!.Value() is not { } named) return;
        await named.Set(new data.@this(named.Name, element.Empty(Context), element, context: Context), Context);
    }

    public partial data.@this Collection { get; init; }
    /// <summary>The variable each element is bound to — <c>%item%</c> when not named.</summary>
    [Default("item")]
    public partial data.@this<app.type.item.variable.@this>? Item { get; init; }
    /// <summary>The variable each key (dict key or list index) is bound to — unbound when not named.</summary>
    public partial data.@this<app.type.item.variable.@this>? Key { get; init; }

    /// <summary>Runs its items side by side (<c>foreach %goals% in parallel(cpu: 2), call goal X, write to %task%</c>):
    /// each item as a task, at most <c>cpu</c> at once. The loop answers a task at once and the step goes on; waited
    /// for, the task answers <c>{count, complete}</c> once every item has run, or the first item's failure. Left out,
    /// the items run one after another.</summary>
    public partial data.@this<app.type.item.parallel.@this>? Parallel { get; init; }

    public async Task<data.@this> Start()
    {
        // Item is %item% unless the step names one ([Default])
        if (await Item!.Value() is not { } itemVariable) return Item;
        var keyVariable = Key is { IsInitialized: true } ? await Key.Value() : null;

        // The actions after this foreach in the step's chain: the keeps at its end that hold the step's answer
        // (`write to %r%`) keep the loop's, and the actions between the loop and them are what each item runs (a
        // `set %seen% = %item%` among them). The Handled flag below stops the outer chain from re-running them.
        var bodyActions = Step?.Code.After(__action!) ?? [];
        var keeps = bodyActions.Reverse().TakeWhile(action => action.IsAnswer).Reverse().ToList();
        var body = bodyActions.Take(bodyActions.Count - keeps.Count).ToList();

        // in parallel the loop answers a task at once; one after another, {count, complete} once the items have run
        var answered = Parallel != null && await Parallel.ToBooleanAsync() && await Parallel.Value() is { } side && Step?.Goal is { } goal
            ? Context.Ok(Started(goal, side, itemVariable, keyVariable, body))
            : await Run(itemVariable, keyVariable, body);
        if (answered.Returned || !answered.Success) return answered;

        // what its keeps keep is the loop's answer, as %!data% is an action's once it has answered
        if (keeps.Count > 0) await Context.Variable.Set("!data", answered);
        foreach (var keep in keeps)
        {
            answered = await keep.Follow(answered, Context);
            if (!answered.Success) return answered;
        }
        answered.Handled = bodyActions.Count > 0;
        return answered;
    }

    /// <summary>The items one after another, each run of the body a call whose frame binds %item% (and %key%): the
    /// loop's own names end with it — an outer loop's %item% is its own again after a nested one — while the body's
    /// other writes (a running total) reach the caller. Answers <c>{count, complete}</c>, or the first return or
    /// failure of the body.</summary>
    private async Task<data.@this> Run(global::app.type.item.variable.@this itemVariable,
        global::app.type.item.variable.@this? keyVariable, System.Collections.Generic.IReadOnlyList<global::app.goal.step.action.@this> body)
    {
        // A value-less collection (the null citizen or an absent slot) iterates zero times; an empty list/dict
        // enumerates to zero naturally. The null citizen Peeks itself (IsNull), absent Peeks null.
        var collectionValue = await Collection.Value();
        if (collectionValue == null || collectionValue.IsNull || collectionValue.Peek() == null)
            return await Result(count: 0, complete: true);

        int count = 0;
        // Data owns enumeration: dicts yield (dictKey, value), lists yield (index, element)
        foreach (var (key, item) in await Collection.EnumerateItems())
        {
            if (Context.CancellationToken.IsCancellationRequested)
                return await Result(count, complete: false);

            // the element as it is here, in the loop's step, as a set reads a value (a template renders here)
            var settled = await item.Settle();
            if (settled.IsInitialized && !settled.Success) return settled;
            var bound = new List<data.@this>
                { settled.IsInitialized ? settled.Copy(itemVariable.Name) : Context.NotFound(itemVariable.Name) };
            // Optional param: absent slots are non-null Uninitialized (null model), so
            // "was a key named?" is IsInitialized, not a C# null check.
            if (keyVariable != null) bound.Add(key.Copy(keyVariable.Name));

            await using (Context.call.Push(bound))
                foreach (var action in body)
                {
                    var result = await action.Start(Context);
                    if (result.Returned || !result.Success) return result;
                }
            count++;
        }
        return await Result(count, complete: true);
    }

    /// <summary>The loop as a task of its actor's: each item a task of its own, run in a context of its own whose first
    /// frame binds the item (and its key) and reads through to the frame the loop is in, at most
    /// <paramref name="side"/>'s cpu at once; it ends when every item has, answering <c>{count, complete}</c>, or the
    /// first item's failure.</summary>
    private global::app.task.@this Started(global::app.goal.@this goal, global::app.type.item.parallel.@this side,
        global::app.type.item.variable.@this itemVariable, global::app.type.item.variable.@this? keyVariable,
        System.Collections.Generic.IReadOnlyList<global::app.goal.step.action.@this> body)
    {
        var from = Context.call.Current;
        var actor = Context.Actor;
        return actor.Task.Start(goal, async token =>
        {
            // the places items wait for; never disposed here, as an item already started may still be waiting
            var places = new System.Threading.SemaphoreSlim(side.At);
            var items = new List<global::app.task.@this>();
            foreach (var (key, item) in await Collection.EnumerateItems())
            {
                // the element as it is here, in the loop's step, as a set reads a value (a template renders here)
                var settled = await item.Settle();
                if (settled.IsInitialized && !settled.Success) return settled;
                var bound = new List<data.@this>
                    { settled.IsInitialized ? settled.Copy(itemVariable.Name) : Context.NotFound(itemVariable.Name) };
                if (keyVariable != null) bound.Add(key.Copy(keyVariable.Name));
                items.Add(actor.Task.Start(goal, async own =>
                {
                    await places.WaitAsync(own);
                    try
                    {
                        using var child = Context.Child(actor, own);
                        await using (child.call.Isolate(bound, caller: from))
                            foreach (var action in body)
                            {
                                var result = await action.Start(child);
                                if (result.Returned || !result.Success) return result;
                            }
                        return child.Ok();
                    }
                    finally { places.Release(); }
                }));
            }
            var ended = await System.Threading.Tasks.Task.WhenAll(items.Select(item => item.Wait(token)));
            if (ended.FirstOrDefault(result => !result.Success) is { } failed) return failed;
            return await Result(ended.Length, complete: !token.IsCancellationRequested);
        });
    }

    /// <summary>
    /// The foreach result, born as a native <c>dict</c> — <c>count</c> (how many items ran) and
    /// <c>complete</c> (false when cancelled). A plain-data result, so it rides as a dict, not a dedicated type.
    /// </summary>
    private async Task<data.@this> Result(int count, bool complete)
        => await Context.App.type.list["dict"].Create(
            new Dictionary<string, object?> { ["count"] = (long)count, ["complete"] = complete }, Context);
}
