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

    /// <summary>Hard cap on Output recursion depth — a graph deeper than this is almost certainly a
    /// reference cycle. Bounds the self-write so a cycle fails typed, not via stack overflow.</summary>
    private const int MaxOutputDepth = 128;

    /// <summary>Tracks Output recursion depth (AsyncLocal — one chain per serialize walk).</summary>
    private static readonly System.Threading.AsyncLocal<int> _outputDepth = new();

    /// <summary>
    /// Data writes ITSELF to the wire — it owns its <c>@schema</c> layer (its identity), then its
    /// <c>type</c>, then the underlying <c>value</c> (delegated to the item), then <c>properties</c>.
    /// One async pass, resolving lazily at each node — no pre-resolve walk, no Normalize tree. A Data
    /// holding a <c>%ref%</c> resolves it and outputs the RESOLVED value's self-describing form (its
    /// real type+value), not <c>type:variable</c>.
    ///
    /// <para>Depth-guarded: bounds the recursive self-write so a reference cycle (a graph that
    /// contains itself, or a self-referential variable) fails with a typed <c>OutputMaxDepth</c> error
    /// instead of a stack overflow — the guarantee the deleted <c>Normalize</c> walker gave via its
    /// visited-set + depth cap. Every nested <c>Output</c> re-enters here, so the counter tracks true
    /// recursion depth.</para>
    /// </summary>
    public async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, View mode,
        global::app.actor.context.@this? context = null, bool layer = false)
    {
        if (_outputDepth.Value >= MaxOutputDepth)
            throw new global::app.error.AppException(
                $"Output exceeded depth {MaxOutputDepth} — a value graph that deep is almost certainly "
                + "a reference cycle.", "OutputMaxDepth", 500);
        _outputDepth.Value++;
        try { await OutputCore(writer, mode, context, layer); }
        finally { _outputDepth.Value--; }
    }

    private async System.Threading.Tasks.ValueTask OutputCore(
        global::app.channel.serializer.IWriter writer, View mode,
        global::app.actor.context.@this? context, bool layer)
    {
        context ??= _context;

        // A reference binding resolves to its target binding before output — but ONLY for an
        // outbound/wire write (Out/Debug carry the resolved VALUE). A Store write (.pr) preserves
        // the authored ref verbatim — build time has no variables to resolve, and the ref IS the
        // artifact. A self-referential binding (msg → msg) trips the depth guard above.
        if (_item is global::app.variable.@this vref && context != null && mode != View.Store)
        {
            var resolved = await context.Variable.Get(vref.Name);
            if (resolved == null || !resolved.IsInitialized)
                throw new global::app.error.VariableNotFoundException(vref.Name);
            await resolved.Output(writer, mode, context, layer);
            return;
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
                writer.Name("type");
                writer.BeginObject();
                writer.Name("name");
                writer.String(Type.Name);
                if (!string.IsNullOrEmpty(Type.Kind))
                {
                    writer.Name("kind");
                    writer.String(Type.Kind!);
                }
                if (Type.Strict)
                {
                    writer.Name("strict");
                    writer.Bool(true);
                }
                writer.EndObject();
            }
            writer.Name("value");
        }

        // The value writes itself — the type owns its per-format serialization (it holds
        // its own format map and picks by writer.Format, or writes its default form).
        await _item.Output(writer, mode, context);

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
                        await global::app.type.@this.Create(kvp.Value, context).Output(writer, mode, context);
                }
                writer.EndObject();
            }
            writer.EndObject();
        }
    }
}
