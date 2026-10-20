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
        var parser = new global::app.type.item.variable.parser.@this(path, _context!.App.type.list);
        if (parser.Path() is not { } code)
            return _context.Error(parser.Error[0]);
        return await code.Start(Context, this);
    }

    /// <summary>
    /// Writes <paramref name="value"/> as this value's child at <paramref name="key"/> —
    /// <paramref name="isIndex"/> tells a position (<c>[0]</c>) from a member (<c>.name</c>). The item
    /// owns the write; this Data rebinds when the item comes back replaced (a json host materialises
    /// into a dict; a clr host mutates in place, so identity holds). When this Data is a variable's own binding
    /// (<paramref name="keeps"/>), a member its value can't take is kept in its Properties. Answers this Data.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<@this> Set(string key, bool isIndex, object? value, bool keeps = false)
    {
        // Setting a member is a USE: open a source-backed value (a `%cfg%` still raw json, a template
        // container still a wire) so the write lands on the PARSED value, not the raw form — a resting json
        // source parses once and the Data narrows here (805). The write target is the opened value itself —
        // a cacheable source rebinds through the door, but a re-resolving template container (Cacheable=false)
        // never rebinds, so relying on Peek() would land the write on the stale wire. Use the opened value
        // directly and rebind to the written result, which snapshots a template container into its plain
        // resolved form on first write (correct for a dict built up across several sets).
        var before = Error;
        var target = await Open();
        // the read failed (the value didn't parse, or its type declined it): that is the write's answer
        if (Error != null && !ReferenceEquals(Error, before)) return _context?.Error(Error) ?? this;

        if (target is null)
            return _context?.NotFound(key) ?? this;

        // The value takes the child, or answers why it can't — `set %!app.goal.list.setting.foo% = 1` is a program's
        // mistake, answered as one.
        @this written;
        // a value the child refuses outright (a reserved key) says why
        try { written = await target.Set(key, isIndex, value, _context!); }
        catch (global::app.error.AppException ex) { return _context?.Error(ex.Error) ?? this; }
        if (!written.Success)
        {
            if (!written.Error!.Status.IsNotFound) return _context?.Error(written.Error) ?? this;
            // a variable's own binding (keeps) holds a member its value has no place for (`set %name.lang% = "is"`); a
            // plain value inside another has nowhere to keep one
            if (keeps && !isIndex)
            {
                Properties[key] = value is @this given ? await given.Value() : value;
                return this;
            }
            return _context?.Error(new global::app.error.@this(
                $"{written.Error.Message}: %{Path}% is a {target.Type.Name} inside another value, and has nowhere to keep a member",
                "CannotSetChild", 400)) ?? this;
        }
        if (!ReferenceEquals(written.Peek(), Peek())) SetValue(written.Peek());
        return this;
    }
}
