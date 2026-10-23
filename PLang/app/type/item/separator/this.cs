namespace app.type.item.separator;

/// <summary>
/// PLang <c>separator</c> — what cuts a text into pieces (<c>split %x% into lines</c>) or goes between them
/// (<c>join %list% with comma</c>): a named separator (line, comma, tab, space, semicolon) standing for its
/// characters, or any characters as written. It is a kind of <c>text</c>: <c>{text, kind:
/// separator}</c> — a text value holding its own OPEN set of named values, so <c>%line% is text</c> is true.
/// Its named values are its own (the type's hook), not sub-kinds of their own.
/// </summary>
public sealed class @this : global::app.type.item.text.@this, global::app.type.item.ICreate<@this>
{
    // the named separators: name → the characters it stands for; any characters are allowed too (open set).
    private static readonly System.Collections.Generic.Dictionary<string, string> Names =
        new(System.StringComparer.OrdinalIgnoreCase)
        { ["line"] = "\n", ["comma"] = ",", ["tab"] = "\t", ["space"] = " ", ["semicolon"] = ";" };

    // the name a named separator goes by (comma), null for characters as written.
    private readonly string? _name;

    /// <summary>Any characters as written, born holding <paramref name="type"/> — the app's <c>{text, separator}</c>.</summary>
    public @this(string characters, global::app.type.@this type) : base(characters, type) { }

    /// <summary>A named separator — its name and the characters it stands for, born holding <paramref name="type"/>.</summary>
    private @this(string name, string characters, global::app.type.@this type)
        : base(characters, type) => _name = name;

    /// <summary>The characters it stands for — a named one's (<c>line</c> a new line), else its own.</summary>
    public string Characters => ToString();

    /// <summary>A named separator writes its name (<c>line</c>), any other its characters — read back the same.</summary>
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter w) { w.String(_name ?? ToString()); return default; }

    /// <summary>What a separator can be in a step, as the decider is offered it: the named separators. Any
    /// characters are allowed too, so the set is open (the formal reader never refuses a literal against it). A
    /// class's own fact, read with no app: each holds {text, separator} built from the class — until the kinds
    /// item.</summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.item.@this> Offer
        => Names.Select(n => (global::app.type.item.@this)new @this(n.Key, n.Value,
            new global::app.type.@this("text", typeof(@this), "separator"))).ToList();

    /// <summary>A separator holds the text a step quoted by its characters too: <c>by ","</c> written as
    /// <c>comma</c> keeps it.</summary>
    internal override async System.Threading.Tasks.ValueTask<bool> Holds(string quoted, global::app.actor.context.@this context)
        => Characters == quoted || await base.Holds(quoted, context);

    /// <summary>A separator passes through; a text that is exactly a named separator's name is that separator
    /// (whatever case), else its characters as written — from a literal and from a variable's value alike.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this separator) return separator;
        if (((raw as global::app.type.item.@this)?.ToString() ?? raw as string) is not { } text) return null;
        type = type is { kind.IsEmpty: false } ? type : data.Context.App.type.list[typeof(@this)];
        return Names.TryGetValue(text, out var chars) ? new @this(text, chars, type) : new @this(text, type);
    }
}
