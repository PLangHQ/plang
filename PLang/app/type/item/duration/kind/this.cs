namespace app.type.item.duration.kind;

/// <summary>
/// A standard a duration is written in — a kind of <c>duration</c> (<c>short</c> 30s, <c>iso</c> PT30S,
/// <c>dotnet</c> 00:00:30). Each owns both directions of its form: the span a text in it is, and a span's text in
/// it. The forms don't overlap, so a text names its own kind; a new standard is one new folder.
/// </summary>
public abstract class @this : global::app.type.kind.@this
{
    protected @this(string name) : base(name) { }

    protected internal override string Owner => "duration";

    /// <summary>The span <paramref name="text"/> is in this standard; null when it isn't written in it.</summary>
    internal abstract System.TimeSpan? Span(string text);

    /// <summary><paramref name="span"/> written in this standard.</summary>
    internal abstract string Text(System.TimeSpan span);

    /// <summary>A text in this standard, as a duration born with it.</summary>
    public override global::app.type.item.@this? Parse(object raw, global::app.actor.context.@this ctx)
        => raw is string text && Span(text.Trim()) is { } span ? new duration.@this(span, this) : null;
}
