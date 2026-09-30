namespace app.error.list;

/// <summary>
/// Errors observed, in the order they came — a frame's (<c>%!callStack.Current.Errors%</c>) or the whole run's
/// (<c>%!callStack.Audit%</c>, every error at every frame, recovered or not; it survives Pop). A plang value:
/// written as its errors, navigated by position (<c>[0]</c>) and <c>Count</c>.
///
/// <para>Owns its append lock: parallel goal.call branches under Task.WhenAll can <see cref="Add"/> at once.
/// Iteration takes a snapshot, so a concurrent Add never throws. Unbounded for its owner's lifetime.</para>
/// </summary>
public sealed class @this : global::app.type.item.@this, IReadOnlyList<global::app.error.Error>
{
    private readonly List<global::app.error.Error> _entries = new();
    private readonly object _lock = new();

    /// <summary>A structure — navigated by position.</summary>
    public override bool IsLeaf => false;

    /// <summary>Held by position.</summary>
    public override bool IsSequence => true;

    /// <summary>Appends <paramref name="error"/> — safe under parallel branches.</summary>
    public void Add(global::app.error.Error error)
    {
        lock (_lock) _entries.Add(error);
    }

    /// <summary>The most recent error — null when none. A frame that failed twice is in play on the second.</summary>
    public global::app.error.Error? Newest
    {
        get { lock (_lock) return _entries.Count == 0 ? null : _entries[^1]; }
    }

    // How many it holds, as C# counts them.
    private int Size
    {
        get { lock (_lock) return _entries.Count; }
    }

    int IReadOnlyCollection<global::app.error.Error>.Count => Size;

    /// <summary>How many errors it holds.</summary>
    public global::app.type.item.number.@this Count => Size;

    public global::app.error.Error this[int index]
    {
        get { lock (_lock) return _entries[index]; }
    }

    public IEnumerator<global::app.error.Error> GetEnumerator()
    {
        global::app.error.Error[] snapshot;
        lock (_lock) snapshot = _entries.ToArray();
        return ((IEnumerable<global::app.error.Error>)snapshot).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Written as its errors, each in its own face.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        Write(writer);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    public override void Write(global::app.type.format.IWriter writer)
    {
        var errors = this.ToArray();
        writer.BeginArray(errors.Length);
        foreach (var error in errors) error.Write(writer);
        writer.EndArray();
    }

    /// <summary>A position (<c>[0]</c>) is that error; a member (<c>.Count</c>, <c>.Newest</c>) is read off the list.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key, bool isIndex)
    {
        if (isIndex && int.TryParse(key, out var at))
            return new(at >= 0 && at < Size
                ? new global::app.data.@this(key, this[at], parent: parent)
                : parent.Context.NotFound(key));
        return Get(parent, key);
    }

    /// <summary>Iterates as (position, error) pairs.</summary>
    public override IEnumerable<(global::app.data.@this key, global::app.data.@this value)> EnumerateItems(
        global::app.actor.context.@this? context)
    {
        var i = 0;
        foreach (var error in this)
            yield return (new global::app.data.@this("", i++, context: context), new global::app.data.@this("", error, context: context));
    }
}
