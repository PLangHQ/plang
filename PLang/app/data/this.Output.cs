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
    /// <summary>
    /// Hard cap on Normalize depth. Mirrors <c>MaxRehydrationDepth</c> in
    /// <see cref="this.Transport.cs"/> — past this is almost certainly an
    /// unbounded structure rather than legitimate nesting.
    /// </summary>
    private const int MaxNormalizeDepth = 128;

    /// <summary>Cap on variable-resolution recursion at output. A self-referential variable chain
    /// (<c>a=%b%</c>, <c>b=%a%</c>) resolves forever; this bounds it to a typed error. NOT a general
    /// graph-depth cap — Output serializes plang TREES (from wire/literals; the writer fail-closes on
    /// any non-plang value), so variable resolution is the only output recursion that can cycle.</summary>
    private const int MaxResolveDepth = 50;

    /// <summary>Tracks variable-resolution recursion depth (AsyncLocal — one chain per serialize walk).</summary>
    private static readonly System.Threading.AsyncLocal<int> _outputDepth = new();

    /// <summary>
    /// Data writes ITSELF to the wire — it owns its <c>@schema</c> layer (its identity), then its
    /// <c>type</c>, then the underlying <c>value</c> (delegated to the item), then <c>properties</c>.
    /// One async pass, resolving lazily at each node — no pre-resolve walk, no Normalize tree. A Data
    /// holding a <c>%ref%</c> resolves it and outputs the RESOLVED value's self-describing form (its
    /// real type+value), not <c>type:variable</c>.
    /// </summary>
    public async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, View mode,
        global::app.actor.context.@this? context = null, bool layer = false)
    {
        context ??= _context;

        // A reference binding resolves to its target binding before output — but ONLY for an
        // outbound/wire write (Out/Debug carry the resolved VALUE). A Store write (.pr) preserves
        // the authored ref verbatim. This resolution is the ONE output recursion that can cycle
        // (a=%b%, b=%a%) — guarded so it fails typed, not via stack overflow. Every other value graph
        // is a tree (the writer fail-closes on any non-plang value), so nothing else recurses unbounded.
        if (_item is global::app.variable.@this vref && context != null && mode != View.Store)
        {
            if (_outputDepth.Value++ > MaxResolveDepth)
            {
                _outputDepth.Value = 0;
                throw new global::app.error.AppException(
                    $"self-referential variable '{vref.Name}' on output", "OutputSelfReference", 500);
            }
            try
            {
                var resolved = await context.Variable.Get(vref.Name);
                if (resolved == null || !resolved.IsInitialized)
                    throw new global::app.error.VariableNotFoundException(vref.Name);
                await resolved.Output(writer, mode, context, layer);
                return;
            }
            finally { _outputDepth.Value--; }
        }

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
            if (!Type.IsNull)
            {
                // The type serializes ITSELF — {name, kind?, strict?, template?} — through its own
                // Output. One owner of the type-entity wire shape (app.type.@this.Output); the Data
                // writer does not re-implement it.
                writer.Name("type");
                await Type.Output(writer, mode, context);
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
        if (mode == View.Store || RawUntouched)
            await _item.Output(writer, mode, context);
        else
            await (await Value()).Output(writer, mode, context);

        if (writer.EmitsSchema)
        {
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
                        await context.App.Type.Create(kvp.Value, context).Output(writer, mode, context);
                }
                writer.EndObject();
            }
            writer.EndObject();
        }
    }
}
