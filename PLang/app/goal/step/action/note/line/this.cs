using text = global::app.type.item.text.@this;

namespace app.goal.step.action.note.line;

/// <summary>
/// One line of an action's notes, read once by the notes (<see cref="note.@this"/>). A named line says what it is
/// about (<see cref="Name"/>: one of the action's properties, or <c>Returns</c>, its value), what it means
/// (<see cref="Prose"/>), how a step says it (<see cref="Say"/>) and when the builder writes it
/// (<see cref="Builder"/>). A free line (prose, a bullet, a heading, a line of code) names nothing: it is its
/// characters, as written, in <see cref="Prose"/>. Each template shows the fields its reader needs.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    protected internal override global::app.type.@this Type => new(typeof(@this));

    /// <summary>A structure, not a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>What the line is about — a property's name, or <c>Returns</c>; null for a free line.</summary>
    [global::app.Out]
    public text? Name { get; }

    /// <summary>What it means; a free line's characters, as written.</summary>
    [global::app.Out]
    public text Prose { get; }

    /// <summary>How a step says it (<c>· say:</c>); null when the line doesn't say.</summary>
    [global::app.Out]
    public text? Say { get; }

    /// <summary>When the builder writes it (<c>· builder:</c>); null when the line doesn't say.</summary>
    [global::app.Out]
    public text? Builder { get; }

    /// <summary>What the decider is asked about it, when its action may be the step's (<c>· ask:</c>) — an option the
    /// step's words choose; null when the line asks nothing.</summary>
    [global::app.Out]
    public text? Ask { get; }

    /// <summary>A free line: its characters, as written.</summary>
    public @this(text prose) { Prose = prose; }

    /// <summary>A named line.</summary>
    public @this(text name, text prose, text? say, text? builder, text? ask = null)
    {
        Name = name;
        Prose = prose;
        Say = say;
        Builder = builder;
        Ask = ask;
    }
}
