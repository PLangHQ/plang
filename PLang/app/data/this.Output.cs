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
            await Output(new global::app.type.item.kind.json.Writer(utf8, context.App.type.list, view, emitsSchema: true), view, context, layer: true);
        return global::app.module.crypto.type.hash.@this.Of(bytes.ToArray(), kind);
    }

    /// <summary>
    /// Data writes ITSELF to the wire — it owns its <c>@schema</c> layer (its identity), then its
    /// <c>type</c>, then the underlying <c>value</c> (delegated to the item), then <c>properties</c>.
    /// One async pass, resolving lazily at each node — no pre-resolve walk, no Normalize tree. A Data
    /// holding a <c>%ref%</c> resolves it and outputs the RESOLVED value's self-describing form (its
    /// real type+value), not <c>type:variable</c>.
    /// </summary>
    public async System.Threading.Tasks.ValueTask Output(
        global::app.type.format.IWriter writer, View mode,
        global::app.actor.context.@this? context = null, bool layer = false)
    {
        context ??= _context;

        // A reference binding resolves to its target binding before output — but ONLY for an
        // outbound/wire write (Out/Debug carry the resolved VALUE). A Store write (.pr) preserves
        // the authored ref verbatim. This resolution can cycle (a=%b%, b=%a%) — guarded so it fails
        // typed, not via stack overflow. The app is the other graph that holds itself (through its
        // asker's memory) and guards its own write; every other value graph is a tree (the writer
        // fail-closes on any non-plang value).
        if (_item is global::app.type.item.variable.@this vref && context != null && mode != View.Store)
        {
            if (_outputDepth.Value++ > MaxResolveDepth)
            {
                _outputDepth.Value = 0;
                throw new global::app.error.AppException(
                    $"self-referential variable '{vref.Name}' on output", "OutputSelfReference", 500);
            }
            try
            {
                var resolved = await vref.Start(context);
                if (!resolved.IsInitialized)
                    throw new global::app.error.VariableNotFoundException(vref.Name);
                await resolved.Output(writer, mode, context, layer);
                return;
            }
            finally { _outputDepth.Value--; }
        }

        // What this Data holds: its value, or — when it failed — its error, which writes its own face.
        global::app.type.item.@this held = Error ?? _item;

        // Only the self-describing wire (application/plang) opens the type envelope around
        // the value; a bare format (json, text) writes the value alone (type inferred on
        // read). The value-write below is the SAME either way.
        if (writer.EmitsSchema)
        {
            writer.BeginObject();
            // @schema is the LAYER marker (data vs signature/encryption/compression) — written
            // ONLY at a layer boundary (the top payload). A nested typed value (a dict entry,
            // the value slot's children) carries type+value without it.
            if (layer)
            {
                writer.Name(global::app.data.@this.WireSchema);
                writer.String(global::app.data.@this.WireSchemaData);
            }
            // The binding label (name) rides on EVERY Store-view Data — .pr action params (nested,
            // not a layer) and local persistence bind by name. The Out wire omits names entirely
            // (a server's binding label is not API surface).
            if (mode == View.Store)
            {
                writer.Name("name");
                writer.String(Name);
            }
            var type = Error?.Type ?? Type;
            if (!type.IsNull)
            {
                // The type slot is the type as the writer's kind shows it: in plang its identity
                // {name, kind?, strict?, template?} (plang's pair for a type) — through the one door
                // every value is written by.
                writer.Name("type");
                await writer.Kind.pair.Output(type, writer, mode, context);
            }
            writer.Name("value");
        }

        // Materialize-and-emit — the ONE door (Load() died here). A RawUntouched
        // byte-passthrough (`read file.json → %json% → write out %json%`) emits its
        // bytes VERBATIM, never parsed — the value is not opened. Store preserves refs
        // and reference-fundamental source-forms verbatim (no %var% render, no byte
        // load). Out renders %var%/templates and loads reference-fundamental bytes at
        // the leaf via the value door, then the materialized item writes itself (the
        // sync leaf reads the now-loaded bytes).
        // The value writes ITSELF (OBP: each item owns its render) — no pre-materialise through
        // Value. An untouched source relays its raw verbatim; a materialised template renders
        // itself; a leaf writes itself. Store vs Out rides in `mode`, honoured by each item. It is written
        // through the writer's kind: the kind's pair for it when it has one (a list in formal), else itself.
        await writer.Kind.pair.Output(held, writer, mode, context);

        if (writer.EmitsSchema)
        {
            // A stored row names the variables its value holds, each as its text and code, parsed
            // once at build: loading reads them, never parses.
            if (mode == View.Store && _item.HasVariable)
            {
                writer.Name("variable");
                new global::app.type.item.variable.serializer.Entry().Write(writer, _item.Variable);
            }

            // properties — nested object, omitted when empty.
            if (Properties.Count > 0)
            {
                writer.Name("properties");
                writer.BeginObject();
                foreach (var kvp in Properties)
                {
                    writer.Name(kvp.Key);
                    if (kvp.Value is global::app.data.@this pd)
                        await pd.Output(writer, mode, context);
                    else
                        await global::app.type.item.@this.Create(kvp.Value, context).Output(writer, mode, context);
                }
                writer.EndObject();
            }
            writer.EndObject();
        }
    }
}
