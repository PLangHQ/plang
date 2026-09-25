namespace app.goal.step.action.modifier.list;

/// <summary>
/// The modifiers wrapping one action (<c>action.Modifier</c>), outermost first — the order they are
/// written in formal (<c>error.handle(…) { cache.wrap(…) { file.read(…) } }</c>). The list owns how they
/// compose around the action: <see cref="Wrap"/> folds them right to left, each modifier wrapping the one
/// inside it, and <c>on error</c> clauses written one after another wrap once, together, as ONE try/catch
/// asked in the order written. No sort: the written order is the order.
/// </summary>
public sealed class @this : IReadOnlyList<modifier.@this>
{
    private readonly List<modifier.@this> _items = new();

    public @this() { }

    public @this(IEnumerable<modifier.@this> modifiers) => _items.AddRange(modifiers);

    /// <summary>The next modifier inward — the written order.</summary>
    public void Add(modifier.@this modifier) => _items.Add(modifier);

    /// <summary>Puts <paramref name="modifier"/> at <paramref name="index"/> — a wrap read from outside in
    /// takes its place outermost (index 0).</summary>
    public void Insert(int index, modifier.@this modifier) => _items.Insert(index, modifier);

    public void Clear() => _items.Clear();

    public int Count => _items.Count;

    public modifier.@this this[int index] => _items[index];

    public IEnumerator<modifier.@this> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// The modifiers around <paramref name="inner"/> — right to left, the innermost modifier wrapping the
    /// action first, so the first modifier is outermost. <c>on error</c> clauses written one after another
    /// are ONE try/catch (<see cref="Catch"/>). Answers the wrapped call, or the error a modifier gave
    /// while wrapping.
    /// </summary>
    public async Task<(Func<Task<global::app.data.@this>>? Wrapped, global::app.error.Error? Error)> Wrap(
        Func<Task<global::app.data.@this>> inner, global::app.actor.context.@this context)
    {
        var execute = inner;
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            int first = i;
            if (_items[i].Catches)
                while (first > 0 && _items[first - 1].Catches) first--;

            var (wrapped, error) = _items[i].Catches
                ? await Catch(_items.GetRange(first, i - first + 1), execute, context)
                : await _items[i].Wrap(execute, context);
            if (error != null) return (null, error);
            execute = wrapped!;
            i = first;
        }
        return (execute, null);
    }

    /// <summary>The <c>on error</c> clauses written one after another, around <paramref name="inner"/> as ONE
    /// try/catch: a failure is offered to each clause in the order written; the first whose filters match
    /// handles it and the others never see it — what it returns (a retry's result, its recovery's, a throw
    /// from that recovery) leaves the step.</summary>
    private static async Task<(Func<Task<global::app.data.@this>>? Wrapped, global::app.error.Error? Error)> Catch(
        List<modifier.@this> clauses, Func<Task<global::app.data.@this>> inner, global::app.actor.context.@this context)
    {
        var handlers = new List<(modifier.@this Clause, global::app.module.ICatch Handler)>();
        foreach (var clause in clauses)
        {
            var (handler, error) = await clause.Handler(context);
            if (error != null) return (null, error);
            handlers.Add((clause, (global::app.module.ICatch)handler!));
        }
        return (async () =>
        {
            var result = await inner();
            if (result.Success) return result;
            foreach (var (clause, handler) in handlers)
            {
                if (await handler.Catch(result, inner, context) is not { } handled) continue;
                if (!handled.Success) clause.Recorded(handled.Error!, context);
                return handled;
            }
            return result;
        }, null);
    }
}
