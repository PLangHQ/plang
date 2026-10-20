namespace app.error.list;

/// <summary>
/// Errors observed, in the order they came — a frame's (<c>%!callStack.Current.Errors%</c>) or the whole run's
/// (<c>%!app.error.list%</c>, the call stack's audit: every error at every frame, recovered or not; it survives
/// Pop). A <c>list&lt;error&gt;</c>: written as its errors, navigated by position (<c>[0]</c>) and <c>Count</c>.
///
/// <para>The list owns its append lock: parallel goal.call branches under Task.WhenAll can add at once, and a
/// walk reads a snapshot. Unbounded for its owner's lifetime.</para>
/// </summary>
public sealed class @this : global::app.type.item.list.@this<global::app.error.@this>, IReadOnlyList<global::app.error.@this>
{
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>The most recent error — null when none. A frame that failed twice is in play on the second.</summary>
    public global::app.error.@this? Newest => Items().LastOrDefault();

    int IReadOnlyCollection<global::app.error.@this>.Count => CountRaw;

    global::app.error.@this IReadOnlyList<global::app.error.@this>.this[int index] => this[index];

    public IEnumerator<global::app.error.@this> GetEnumerator() => Items().ToList().GetEnumerator();

    /// <summary>Written as its errors, each in its own face — in any format, bare json too.</summary>
    public override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        var errors = Items().ToArray();
        writer.BeginArray(errors.Length);
        foreach (var error in errors) await writer.Kind.pair.Output(error, writer, mode, context);
        writer.EndArray();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
