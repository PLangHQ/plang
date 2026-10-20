using app;

namespace app.data;

/// <summary>
/// Data writes ITSELF to the wire — each type emits its own form via
/// <see cref="@this.Output"/> / <c>item.Output</c>. One async pass, resolving
/// lazily at each node — no pre-resolve walk, no Normalize tree.
///
/// <para>Bounded: a depth cap on the self-write prevents stack overflow on a
/// reference cycle (a graph that contains itself). It raises a typed error hard
/// at serialize-time — no silent truncation.</para>
/// </summary>
public partial class @this
{
    /// <summary>Cap on variable-resolution recursion at output. A self-referential variable chain
    /// (<c>a=%b%</c>, <c>b=%a%</c>) resolves forever; this bounds it to a typed error. NOT a general
    /// graph-depth cap — Output serializes plang TREES (from wire/literals; the writer fail-closes on
    /// any non-plang value). Two writes can meet themselves: variable resolution (bounded here) and the
    /// app, which its asker's memory can hold — the app guards its own write (met again, it writes its
    /// name).</summary>
    private const int MaxResolveDepth = 50;

    /// <summary>Tracks variable-resolution recursion depth (AsyncLocal — one chain per serialize walk).</summary>
    private static readonly System.Threading.AsyncLocal<int> _outputDepth = new();

    /// <summary>
    /// The digest of this Data as it crosses the wire in <paramref name="view"/> — the bytes its own <see cref="Output"/>
    /// writes at a layer (its <c>@schema</c>, type, value, properties; no signature), hashed by
    /// <paramref name="kind"/>. What a signature binds: sign and verify both write the same bytes, so they agree. A
    /// value hashed for a person (<c>crypto.hash</c>) is its own bytes, not these.
    /// </summary>
    internal async System.Threading.Tasks.ValueTask<global::app.module.crypto.type.hash.@this> Digest(View view,
        global::app.module.crypto.type.hash.kind.@this kind, global::app.actor.context.@this context)
    {
        using var bytes = new System.IO.MemoryStream();
        await using (var utf8 = new System.Text.Json.Utf8JsonWriter(bytes))
            await Output(new global::app.type.item.wire.kind.plang.Writer(utf8, view, context), layer: true);
        return global::app.module.crypto.type.hash.@this.Of(bytes.ToArray(), kind);
    }

    /// <summary>
    /// Data writes ITSELF to the wire — it owns its <c>@schema</c> layer (its identity), then its
    /// <c>type</c>, then the underlying <c>value</c> (delegated to the item), then <c>properties</c>.
    /// One async pass, resolving lazily at each node — no pre-resolve walk, no Normalize tree.
    /// <paramref name="layer"/>: this Data begins a layer of plang's own format.
    /// </summary>
    public async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, bool layer = false)
    {
        // A Data whose value holds variables (a template, a reference) is the view's to write: kept as written (Store),
        // used (Out), or both (Debug). It is decided here, before the record, because what crosses (Out) writes the
        // record of the Data the value resolves to — its type, not the template's. Using one can cycle (a=%b%, b=%a%) —
        // guarded so it fails typed, not via stack overflow. The app is the other graph that holds itself (through its
        // asker's memory) and guards its own write; every other value graph is a tree.
        if (Error == null && HasVariable)
        {
            if (_outputDepth.Value++ > MaxResolveDepth)
            {
                _outputDepth.Value = 0;
                throw new global::app.error.AppException(
                    $"self-referential variable '{Name}' on output", "OutputSelfReference", 500);
            }
            try { await writer.View.Write(this, writer, layer); }
            finally { _outputDepth.Value--; }
            return;
        }

        // What this Data holds: its value, or — when it failed — its error, which writes its own face.
        global::app.type.item.@this value = Error ?? _item;

        // The Data tells the writer what it is; the writer decides how much to show (plang's writer its record, a
        // bare format only the value). The value writes itself.
        await writer.BeginRecord(this, layer);
        await value.Output(writer);
        await writer.EndRecord(this);
    }
}
