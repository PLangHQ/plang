using app;

namespace app.data;

/// <summary>
/// Data writes ITSELF to the wire — each type emits its own form via
/// <see cref="@this.Output"/> / <c>item.Output</c>. One async pass, resolving
/// lazily at each node — no pre-resolve walk, no Normalize tree.
///
/// <para>Bounded: the view that uses a value (Out) caps how deep values that name each other go, so a reference cycle
/// raises a typed error at serialize-time — no silent truncation.</para>
/// </summary>
public partial class @this
{
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
    public System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, bool layer = false)
        // The view writes it: its record around what it holds (its value, or — when it failed — its error); one whose value
        // holds variables kept as written (Store), used (Out), or both (Debug). What crosses writes the record of the
        // Data the value resolves to — its type, not the template's.
        => writer.View.Write(this, writer, layer);
}
