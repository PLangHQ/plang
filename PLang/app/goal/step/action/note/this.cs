namespace app.goal.step.action.note;

/// <summary>
/// An action's notes, read: its <c>{Name}.notes.md</c>, each line read once into a <see cref="line.@this"/>, in the
/// file's order. A line <c>Name — prose · say: … · builder: …</c> names what it is about: one of the action's
/// properties, or <c>Returns</c>, the action's value. Any other line (prose, a bullet, a heading, a line inside a
/// code fence) is a free line. The notes are born unread with their action and read the first time they are used,
/// once. Their warnings are what the notes and the action's properties don't agree on — a line naming no property,
/// a property no line names — found here and nowhere else.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    // The name of the line that says what the action returns.
    private const string Result = "Returns";

    // Between a line's prose and each tag after it.
    private const string Tag = " · ";

    // A line that opens or closes a code fence.
    private const string Fence = "```";

    // A named line: the name, then " — ", then the rest.
    private static readonly System.Text.RegularExpressions.Regex Named = new(@"^([A-Za-z_]\w*) — (.*)$");

    private readonly global::app.goal.step.action.@this _action;
    private readonly global::app.type.item.file.@this _file;

    // Null until the notes are read.
    private System.Collections.Generic.IReadOnlyList<line.@this>? _line;
    private global::app.warning.list.@this _warning = new();

    /// <summary>The notes of <paramref name="action"/>, in <paramref name="file"/> — nothing read yet.</summary>
    internal @this(global::app.goal.step.action.@this action, global::app.type.item.file.@this file)
    {
        _action = action;
        _file = file;
    }

    protected internal override global::app.type.@this Type => new(typeof(@this));

    /// <summary>A structure, not a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>The lines, in the file's order; none before the notes are read, or when the action has none.</summary>
    [global::app.Out]
    public System.Collections.Generic.IReadOnlyList<line.@this> Line => _line ?? [];

    /// <summary>The line that says what the action returns (<c>Returns — …</c>); null when none does.</summary>
    public line.@this? Returns => Line.FirstOrDefault(line => line.Name?.ToString() == Result);

    /// <summary>What the notes and the action's properties don't agree on: a line naming no property, a property
    /// no line names.</summary>
    [global::app.Out]
    public global::app.warning.list.@this Warning => _warning;

    /// <summary>The notes read, once: the file's lines, each a named line or a free one, then what they and the
    /// action's properties don't agree on. An action with no notes file has no lines. A file that can't be read
    /// fails <paramref name="data"/>.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        if (_line != null) return this;
        var read = new System.Collections.Generic.List<line.@this>();
        if (await _file.AsBooleanAsync(data.Context))
        {
            var sample = await _file.Content(data.Context);
            if (!sample.Success) { data.Fail(sample.Error!); return Absent; }
            var fenced = false;
            foreach (var text in _file.ContentText().Split('\n').Select(text => text.TrimEnd('\r')))
            {
                var fence = text.TrimStart().StartsWith(Fence, System.StringComparison.Ordinal);
                read.Add(fenced || fence ? new line.@this(new global::app.type.item.text.@this(text)) : Read(text));
                if (fence) fenced = !fenced;
            }
        }
        var warning = new global::app.warning.list.@this();
        var named = read.Where(line => line.Name != null).Select(line => line.Name!.ToString()).ToHashSet();
        foreach (var name in named.Where(name => name != Result && _action[name] is null))
            warning.Add(new global::app.warning.@this { Key = "NoteWithoutProperty",
                Message = $"{_action.Module.Name}.{_action.Name}: the notes line '{name}' names no property" });
        foreach (var property in _action.Property.Where(property => !named.Contains(property.Name)))
            warning.Add(new global::app.warning.@this { Key = "PropertyWithoutNote",
                Message = $"{_action.Module.Name}.{_action.Name}: the property '{property.Name}' has no notes line" });
        _warning = warning;
        _line = read;
        return this;
    }

    /// <summary>The notes are read before a member is reached: <c>%action.Note.Warning%</c> sees the file's.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
    {
        await Value(parent);
        return parent.Success ? await base.Get(parent, key) : parent;
    }

    // A line outside a fence: `Name — …` is a named line, its prose running to the first tag; `say:`, `builder:` and
    // `ask:` tags follow, each after " · ". A part after " · " that is no tag stays in the prose. Anything else is a
    // free line.
    private line.@this Read(string text)
    {
        if (Named.Match(text) is not { Success: true } named) return new line.@this(new global::app.type.item.text.@this(text));
        var part = named.Groups[2].Value.Split(Tag);
        var prose = part[0];
        global::app.type.item.text.@this? say = null, builder = null, ask = null;
        foreach (var tag in part.Skip(1))
        {
            if (tag.StartsWith("say:", System.StringComparison.Ordinal)) say = new(tag["say:".Length..].Trim());
            else if (tag.StartsWith("builder:", System.StringComparison.Ordinal)) builder = new(tag["builder:".Length..].Trim());
            else if (tag.StartsWith("ask:", System.StringComparison.Ordinal)) ask = new(tag["ask:".Length..].Trim());
            else prose += Tag + tag;
        }
        return new line.@this(new global::app.type.item.text.@this(named.Groups[1].Value), new global::app.type.item.text.@this(prose), say, builder, ask);
    }
}
