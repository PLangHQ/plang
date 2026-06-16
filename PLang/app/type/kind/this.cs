namespace app.type.kind;

/// <summary>
/// A kind value — the subtype token ("json", "jpg", "md") that names HOW a raw
/// form decodes. Content off I/O rests as <c>binary</c> carrying a kind; on
/// access the kind names the type the bytes narrow to, and that type's reader
/// does the parse.
///
/// <para>The kind owns that mapping: a kind-specific reader names its owner
/// (<c>json→item</c>, <c>csv→table</c> — the reader registry); otherwise the
/// format family answers (<c>jpg→image</c>, <c>md→text</c>). Unknown kinds stay
/// <c>binary</c> (nothing decodes them).</para>
/// </summary>
public sealed class @this
{
    public string Name { get; }
    private readonly actor.context.@this _context;

    public @this(string name, actor.context.@this context)
    {
        Name = name;
        _context = context ?? throw new System.ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// The type a value of this kind narrows to once decoded — the reader's
    /// owner type when a kind-specific reader exists, else the format family,
    /// else <c>binary</c> (undecodable, stays bytes).
    /// </summary>
    public global::app.type.@this Type
    {
        get
        {
            string name = _context.App.Type.Readers.TypeOf(Name)
                          ?? _context.App.Format.TypeOf(Name)
                          ?? "binary";
            return new global::app.type.@this(name, Name) { Context = _context };
        }
    }
}
