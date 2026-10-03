namespace app.type.item.separator;

/// <summary>
/// PLang <c>separator</c> value — what cuts a text into pieces (<c>split %x% into lines</c>) or goes between them
/// (<c>join %list% with comma</c>): a named separator (line, comma, tab, space, semicolon), standing for its
/// characters, or any characters as written. A text that is exactly a separator's name is that separator.
/// </summary>
[global::app.Attributes.PlangType("separator")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Shape => "string";

    private readonly kind.@this? _named;

    /// <summary>The named separator.</summary>
    public @this(kind.@this named) : this(named.Characters) => _named = named;

    /// <summary>The characters as written.</summary>
    public @this(string characters) => Characters = characters;

    /// <summary>The characters it stands for — a named one's (<c>line</c> a new line), else its own.</summary>
    public string Characters { get; }

    public override bool IsLeaf => true;

    /// <summary>A named separator writes its name (<c>line</c>), any other its characters — read back the same.</summary>
    public override void Write(global::app.type.format.IWriter w) => w.String(_named?.Name ?? Characters);

    public override string ToString() => Characters;

    /// <summary>A separator holds the text a step quoted by its characters too: `by ","` written as <c>comma</c> keeps it.</summary>
    internal override async System.Threading.Tasks.ValueTask<bool> Holds(string quoted, global::app.actor.context.@this context)
        => Characters == quoted || await base.Holds(quoted, context);

    /// <summary>A separator passes through; a text is the separator it names, whatever case and by an alias (lines),
    /// else its characters as written — from a literal and from a variable's value alike.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (raw is @this separator) return separator;
        if (((raw as global::app.type.item.@this)?.ToString() ?? raw as string) is not { } text) return null;
        return data.Context?.App.type.list["separator"].kind[text] is kind.@this { IsEmpty: false } named
            ? new @this(named) : new @this(text);
    }
}
