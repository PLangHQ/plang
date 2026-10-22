namespace app.type.item.kind.json;

/// <summary>
/// json content, unread — a json file's characters. Its characters already are json, so a json document takes them as
/// they are (pasted, never quoted); in any other format it is a source's own characters. Made by the json kind
/// (<see cref="@this.Make"/>), which is its format. Asked whether it is empty, it is what it stands for: <c>{}</c> and
/// <c>[]</c> are empty.
/// </summary>
public class source : global::app.type.item.source
{
    /// <summary>json content holding <paramref name="raw"/> (characters, or their UTF-8 bytes), of
    /// <paramref name="type"/>, read by <paramref name="json"/>, off <paramref name="origin"/> when it came from a file —
    /// a template holding <paramref name="variable"/>, the build's own when <paramref name="built"/>.</summary>
    public source(object raw, global::app.type.@this type, @this json, global::app.type.item.path.@this? origin = null,
        System.Collections.Generic.IReadOnlyList<global::app.type.item.variable.@this>? variable = null, bool built = false)
        : base(raw, type, variable, origin, built, json) { }

    // What the characters stand for, once opened, per context that opened them — kept, so a truth asked first and a
    // navigation after parse once; never handed to another context (a typed row inside is born with the one that
    // parsed it). Weakly held: a context gone takes its parse with it.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<global::app.actor.context.@this, global::app.type.item.@this> _opened = new();

    // the characters as they are: bytes are their UTF-8
    private string Characters => Raw as string ?? System.Text.Encoding.UTF8.GetString((byte[])Raw);

    internal override global::app.type.item.@this? Parse(global::app.actor.context.@this ctx)
    {
        if (_opened.TryGetValue(ctx, out var opened)) return opened;
        var parsed = base.Parse(ctx);
        if (parsed != null) _opened.AddOrUpdate(ctx, parsed);
        return parsed;
    }

    /// <summary>Whether it holds anything is what it stands for's answer — opening it is a use (<c>{}</c>, <c>[]</c>,
    /// <c>""</c>, <c>0</c>, <c>false</c> and <c>null</c> are empty). Characters that are no json answer why.</summary>
    public override async System.Threading.Tasks.Task<bool> AsBooleanAsync(global::app.actor.context.@this context)
        => Opened(context) is { } opened ? await opened.AsBooleanAsync(context) : IsTruthy();

    /// <summary>Unread, it drives every comparison it is in: it opens itself first and hands the comparison to what it
    /// stands for, which then ranks as it does.</summary>
    public override int Rank => int.MaxValue;

    /// <summary>Compared, it is what it stands for: opened, what it parses to is compared — two equal json objects are
    /// equal, and json and a dict of the same content too. Characters that are no json answer why.</summary>
    protected override async System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other,
        global::app.actor.context.@this context)
        => Opened(context) is { } opened ? await opened.Compare(other, context) : await base.Order(other, context);

    // What the characters stand for, opened; characters that are no json are the asker's error, saying why.
    private global::app.type.item.@this? Opened(global::app.actor.context.@this context)
    {
        try { return Parse(context); }
        catch (System.Text.Json.JsonException shape)
        {
            throw new global::app.error.AppException(Unread(shape, new global::app.data.@this("", this, context: context)));
        }
    }

    protected internal override System.Threading.Tasks.ValueTask Write(Writer writer)
    {
        writer.Raw(Characters);
        return default;
    }

    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.item.wire.kind.plang.Writer writer)
    {
        writer.Raw(Characters);
        return default;
    }
}
