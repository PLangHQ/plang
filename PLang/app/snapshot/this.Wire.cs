namespace app.snapshot;

/// <summary>
/// Snapshot — its text, both ways: it writes its own form into text, and is made from that text by its own reader.
/// Nothing above it needs to know its layout.
/// </summary>
public sealed partial class @this
{
    /// <summary>
    /// In text a snapshot is its own written form, so what a file or a db row holds reads back as it
    /// (<see cref="serializer.Reader"/>): its object of entries, each entry the Data it is with its type, in the program
    /// form a <c>.pr</c> is (goal's format). Content alone would lose what each entry is (a section reads back as a dict).
    /// The root rides bare: no Data around it, unsigned, in the snapshot's own view (no secret in it).
    /// </summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.item.text.Writer writer)
    {
        var context = writer.Context;
        using var ms = new System.IO.MemoryStream();
        await context.App.type.list["goal"].kind.Encode(ms, context.Ok(this), context, global::app.View.Snapshot);
        writer.String(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
    }

    /// <summary>
    /// The born-with-context creation door (<c>Data.Value&lt;snapshot&gt;</c> dispatches here): a snapshot passes
    /// through; its text (a file read, a db row) is read by the snapshot's own reader (<see cref="serializer.Reader"/>),
    /// the one door from text to a snapshot, in the view it was written in; anything else declines.
    /// </summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this snapshot) return snapshot;
        var context = data.Context;
        var view = global::app.View.Snapshot;
        if (raw is global::app.type.item.text.@this text)
            return text.Read(view.Reader(context.App.type.list[typeof(@this)], null, context), null, context, view) as @this;
        data.Fail(new global::app.error.@this(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name} — a snapshot is made from a snapshot or its text",
            "CreateItemDeclined", 400));
        return null;
    }
}
