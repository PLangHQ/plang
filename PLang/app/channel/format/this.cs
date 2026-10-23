namespace app.channel.format;

/// <summary>
/// The format a channel writes and reads in — a format kind, named as any format is: its name (<c>plang</c>,
/// <c>json</c>), a MIME (<c>application/octet-stream</c>) or an extension (<c>txt</c>, <c>bin</c>, <c>.md</c>). It is
/// written by its first MIME, else its name, so it reads back as the same format. A name no format answers to is
/// refused when it is made.
/// </summary>
[global::app.Attributes.PlangType("format")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The format <paramref name="kind"/> is, born holding <paramref name="type"/> — the app's <c>format</c>.</summary>
    public @this(global::app.type.kind.@this kind, global::app.type.@this type) : base(type) => Kind = kind;

    /// <summary>A channel's setting, never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The kind that writes and reads the channel's content.</summary>
    public global::app.type.kind.@this Kind { get; }

    /// <summary>Whether this is plang's own format — a channel in it speaks plang (a program that runs this plang).</summary>
    internal bool IsPlang => Kind is global::app.type.item.wire.kind.plang.@this;

    /// <summary>A format passes; a name, MIME or extension is the format the type list reads by it. Anything else, or a
    /// name no format answers to, is refused.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this format) return format;
        var named = raw is global::app.type.item.@this { IsLeaf: true } leaf ? leaf.RawText : raw as string;
        var context = data.Context!;
        if (!string.IsNullOrWhiteSpace(named) && context.App.type.list.Format(named) is { } kind)
            return new @this(kind, context.App.type.list[typeof(@this)]);
        data.Fail(new global::app.error.@this(
            $"No format '{named ?? raw?.ToString()}': name one a type reads — a MIME (application/plang), a name (json) or an extension (txt, bin, .md)",
            "FormatNotFound", 400));
        return null;
    }

    /// <summary>One name, written as the format is read back: its first MIME, else its name.</summary>
    public override bool IsLeaf => true;

    public override string? RawText => Kind.Mime.Count > 0 ? Kind.Mime[0] : Kind.Name;

    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.String(RawText!);
        return default;
    }

    public override string ToString() => RawText!;
}
