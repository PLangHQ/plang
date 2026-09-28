namespace app.@event.binding.list;

/// <summary>
/// The bindings of one side of an event (its <see cref="before"/>, or its <see cref="after"/>), in the order they
/// were added. An own event's lists take bindings; the shared empty event's are closed, made so, and never hold
/// one. Starting it reads the list as it stands, so firing an event nothing is bound on costs one read. How the
/// bindings' answers fold into the result is the side's own. Not an item of its own (a plang value named
/// <c>list</c> would be a second type of that name); navigation reaches it as a sequence.
/// </summary>
public abstract class @this : IReadOnlyList<binding.@this>
{
    private readonly bool _open;
    private readonly object _gate = new();
    private binding.@this[] _bindings = [];

    private protected @this(bool open) => _open = open;

    public int Count => _bindings.Length;

    public binding.@this this[int index] => _bindings[index];

    public IEnumerator<binding.@this> GetEnumerator() => ((IEnumerable<binding.@this>)_bindings).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Binds <paramref name="handler"/> here for <paramref name="actor"/>, in <paramref name="scope"/>,
    /// for the items <paramref name="filter"/> takes; answers the binding.</summary>
    internal binding.@this Add(
        System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this, System.Threading.Tasks.Task<global::app.data.@this>> handler,
        global::app.actor.@this actor, binding.Scope scope,
        System.Func<global::app.type.item.@this, global::app.actor.context.@this, bool> filter)
        => Add(side => new binding.@this(side, handler, actor, scope, filter));

    /// <summary>Binds the binding <paramref name="make"/> makes on this side — a kind of binding (a mock) is born
    /// knowing the list it is on; answers it.</summary>
    internal T Add<T>(System.Func<@this, T> make) where T : binding.@this
    {
        if (!_open) throw new InvalidOperationException("the shared empty event takes no binding: bind through the item's own events");
        var binding = make(this);
        lock (_gate) _bindings = [.. _bindings, binding];
        return binding;
    }

    // Takes a binding off (its own Remove).
    internal void Remove(binding.@this binding)
    {
        lock (_gate) _bindings = System.Array.FindAll(_bindings, b => !ReferenceEquals(b, binding));
    }

    /// <summary>
    /// Starts each binding that fires for <paramref name="item"/> while <paramref name="context"/>'s actor runs,
    /// in the order added, on <paramref name="result"/> as it stands. With nothing bound it answers
    /// <paramref name="result"/> as is.
    /// </summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this> Start(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
    {
        // the list as it stands: a binding added while it runs fires the next time
        var bindings = _bindings;
        return bindings.Length == 0 ? new(result) : Run(bindings, item, result, context);
    }

    /// <summary>Starts <paramref name="bindings"/> — the ones that fire for <paramref name="item"/> — and folds
    /// their answers into the result.</summary>
    private protected abstract System.Threading.Tasks.ValueTask<global::app.data.@this> Run(binding.@this[] bindings,
        global::app.type.item.@this item, global::app.data.@this result, global::app.actor.context.@this context);
}
