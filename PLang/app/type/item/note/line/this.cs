namespace app.type.item.note.line;

/// <summary>
/// One line of notes, read once by the notes (<see cref="note.@this"/>). A named line says what it is about
/// (<see cref="Name"/>: one of its parts — an action's property, a member's argument — or <c>Returns</c>, its value), what it means
/// (<see cref="Prose"/>), how a step says it (<see cref="Say"/>) and when the builder writes it
/// (<see cref="Builder"/>). A free line (prose, a bullet, a heading, a line of code) names nothing: it is its
/// characters, as written, in <see cref="Prose"/>. Each template shows the fields its reader needs.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    protected internal override global::app.type.@this Type => new(typeof(@this));

    /// <summary>Plang's own teaching, read by its templates — never a type a program's value is.</summary>
    public static bool Internal => true;

    /// <summary>A structure, not a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>What the line is about — a part's name, or <c>Returns</c>; null for a free line.</summary>
    [global::app.Out]
    public global::app.type.item.text.@this? Name { get; }

    /// <summary>What it means; a free line's characters, as written.</summary>
    [global::app.Out]
    public global::app.type.item.text.@this Prose { get; }

    /// <summary>How a step says it (<c>· say:</c>); null when the line doesn't say.</summary>
    [global::app.Out]
    public global::app.type.item.text.@this? Say { get; }

    /// <summary>When the builder writes it (<c>· builder:</c>); null when the line doesn't say.</summary>
    [global::app.Out]
    public global::app.type.item.text.@this? Builder { get; }

    /// <summary>What the decider is asked about it, when its action may be the step's (<c>· ask:</c>) — an option the
    /// step's words choose; null when the line asks nothing.</summary>
    [global::app.Out]
    public global::app.type.item.text.@this? Ask { get; }

    /// <summary>A free line: its characters, as written.</summary>
    public @this(global::app.type.item.text.@this prose) { Prose = prose; }

    /// <summary>A named line.</summary>
    public @this(global::app.type.item.text.@this name, global::app.type.item.text.@this prose,
        global::app.type.item.text.@this? say, global::app.type.item.text.@this? builder, global::app.type.item.text.@this? ask = null)
    {
        Name = name;
        Prose = prose;
        Say = say;
        Builder = builder;
        Ask = ask;
    }
}
