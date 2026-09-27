namespace app.data;

/// <summary>
/// Data — navigation concern. A path from a value (<c>choices[0].message</c>, <c>[0]</c>,
/// <c>!type</c>, <c>text.grep("x")</c>) is read by the variable parser into the same hops a
/// variable's code runs, started from this Data; a single step is the item's own door.
/// </summary>
public partial class @this
{
    /// <summary>
    /// What this value holds at <paramref name="path"/>. Never returns null — a path that doesn't
    /// parse or doesn't reach answers the reason.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<@this> Get(string path)
    {
        if (string.IsNullOrEmpty(path))
            return this;
        var parser = new global::app.type.item.variable.parser.@this(path);
        if (parser.Path() is not { } code)
            return _context?.Error(parser.Error[0]) ?? NotFound(path);
        return await code.Start(Context, this);
    }

    /// <summary>
    /// Writes <paramref name="value"/> as this value's child at <paramref name="key"/> —
    /// <paramref name="isIndex"/> tells a position (<c>[0]</c>) from a member (<c>.name</c>). The item
    /// owns the write; this Data rebinds when the item comes back replaced (a json host materialises
    /// into a dict; a clr host mutates in place, so identity holds). Answers this Data.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<@this> Set(string key, bool isIndex, object? value)
    {
        // Materialise a source-backed value (a `%cfg%` still raw json, a template container still a
        // wire) so the write lands on the PARSED value, not the raw form. The write target is the
        // materialized value itself — a cacheable source rebinds through the door, but a
        // re-resolving template container (Cacheable=false) never rebinds, so relying on Peek() would
        // land the write on the stale wire. Use the materialized value directly and rebind to the
        // written result, which snapshots a template container into its plain resolved form on
        // first write (correct for a dict built up across several sets).
        var target = await Value();
        if (Error?.Key == "MaterializeFailed") return _context?.Error(Error) ?? this;

        if (target is null)
            return _context?.NotFound(key) ?? this;

        var written = await target.Set(key, isIndex, value, _context);
        if (!ReferenceEquals(written, Peek())) SetValue(written);
        return this;
    }
}
