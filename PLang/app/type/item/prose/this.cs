namespace app.type.item.prose;

/// <summary>
/// What is written about something — a type's description, its example — read when asked, from its pieces: words, and
/// values read through their own door (a teaching file's content). The space around it goes. When a value it is made
/// from holds nothing (no such file), it is empty: falsy, written as nothing.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    /// <summary>plang's own, never offered as a type: a program reads it as the text it is.</summary>
    public static bool Internal => true;

    // words (string) and values (item), in order
    private readonly System.Collections.Generic.IReadOnlyList<object> _pieces;
    // what it reads as when it reads empty
    private readonly @this? _otherwise;

    /// <summary>Prose of <paramref name="pieces"/>: each a string, written as it is, or a value, read when asked.</summary>
    public @this(params object[] pieces) => _pieces = pieces;

    private @this(System.Collections.Generic.IReadOnlyList<object> pieces, @this otherwise)
    {
        _pieces = pieces;
        _otherwise = otherwise;
    }

    /// <summary>This prose, or <paramref name="other"/> when this reads empty — a list of records says what its element
    /// is, else what a list is.</summary>
    public @this Or(@this other) => new(_pieces, _otherwise == null ? other : _otherwise.Or(other));

    protected internal override global::app.type.@this Type => new("text", typeof(global::app.type.item.text.@this));

    public override bool IsLeaf => true;

    /// <summary>Never final — what it is made from can change (a teaching file edited), so it is read at each ask.</summary>
    internal override bool IsFinal => false;

    public override bool Cacheable => false;

    /// <summary>The words it reads as now — its pieces joined, trimmed; no words is no value (null), so a template's
    /// <c>{% if t.Description %}</c> is false for a type that says nothing.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
        => await Text(data.Context!) is { Length: > 0 } words
            ? new global::app.type.item.text.@this(words)
            : global::app.type.item.@null.@this.Instance;

    /// <summary>The words it reads as now, for <paramref name="context"/>.</summary>
    internal async System.Threading.Tasks.ValueTask<string> Text(global::app.actor.context.@this context)
        => await Own(context) is { Length: > 0 } own ? own : _otherwise == null ? "" : await _otherwise.Text(context);

    // its own pieces' words
    private async System.Threading.Tasks.ValueTask<string> Own(global::app.actor.context.@this context)
    {
        var words = new System.Text.StringBuilder();
        foreach (var piece in _pieces)
        {
            if (piece is not global::app.type.item.@this value) { words.Append(piece); continue; }
            var read = new global::app.data.@this("", value, context: context);
            var held = await read.Value();
            if (!read.Success || held == null || held.IsNull) return "";
            var text = (held is @this inner ? await inner.Text(context) : held.ToString() ?? "").Trim();
            if (text.Length == 0) return "";
            words.Append(text);
        }
        return words.ToString().Trim();
    }

    public override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
        => writer.String(context == null ? "" : await Text(context));

    /// <summary>Written without an asker, it has nothing read yet: it is written through <see cref="Output"/>.</summary>
    public override void Write(global::app.type.format.IWriter w) => w.String("");

    public override bool IsTruthy() => _pieces.Count > 0;
}
