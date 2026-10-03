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
    /// into a dict; a clr host mutates in place, so identity holds). A value that can't take the child is
    /// refused: a plain value inside another has nowhere to keep a member. Answers this Data.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<@this> Set(string key, bool isIndex, object? value)
    {
        try { return await Write(key, isIndex, value); }
        catch (System.NotSupportedException ex)
        {
            return _context?.Error(new global::app.error.Error(
                $"{ex.Message}: %{Path}% is a {Peek().Type.Name} inside another value, and has nowhere to keep a member", "CannotSetChild", 400)) ?? this;
        }
    }

    /// <summary>
    /// Writes <paramref name="value"/> as this value's member <paramref name="name"/> when the value takes it; else
    /// keeps it in this Data's own <see cref="Property"/> (<c>set %name.lang% = "is"</c>). The door of a variable's own
    /// binding, chosen by the binding that knows it is one. Answers this Data.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<@this> Keep(string name, object? value)
    {
        try { return await Write(name, isIndex: false, value); }
        catch (System.NotSupportedException)
        {
            Property.Set(name, value is @this given ? await given.Value() : value);
            return this;
        }
    }

    // The write both doors share: the child into the value, the value rebound when it comes back replaced; a value
    // that can't take the child throws NotSupported, for the door to answer.
    private async System.Threading.Tasks.ValueTask<@this> Write(string key, bool isIndex, object? value)
    {
        // Materialise a source-backed value (a `%cfg%` still raw json, a template container still a
        // wire) so the write lands on the PARSED value, not the raw form. The write target is the
        // materialized value itself — a cacheable source rebinds through the door, but a
        // re-resolving template container (Cacheable=false) never rebinds, so relying on Peek() would
        // land the write on the stale wire. Use the materialized value directly and rebind to the
        // written result, which snapshots a template container into its plain resolved form on
        // first write (correct for a dict built up across several sets).
        var before = Error;
        var target = await Value();
        // the read failed (the value didn't parse, or its type declined it): that is the write's answer
        if (Error != null && !ReferenceEquals(Error, before)) return _context?.Error(Error) ?? this;

        if (target is null)
            return _context?.NotFound(key) ?? this;

        global::app.type.item.@this written;
        try { written = await target.Set(key, isIndex, value, _context); }
        // a value the child refuses (an option's value out of its range, a reserved key) says why
        catch (global::app.error.AppException ex) { return _context?.Error(ex.Error) ?? this; }
        if (!ReferenceEquals(written, Peek())) SetValue(written);
        return this;
    }
}
