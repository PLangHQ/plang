namespace app.type.item.input.modifier.list;

/// <summary>
/// The modifier keys an input was made with — <c>%click.modifier%</c>. Read as one value it is the first key held;
/// <c>%click.modifier.list%</c> is every one. It writes the whole list wherever it is shown, and on the screen's
/// input line its code.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    // the screen's wire count for each key, as DevTools counts them
    private const int Alt = 1, Ctrl = 2, Meta = 4, Shift = 8;

    private readonly modifier.@this[] _held;

    /// <summary>A part of an input, never LLM vocabulary of its own — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The keys held in <paramref name="code"/>, the screen's wire count — where an input comes in; born with
    /// the app's <paramref name="types"/>, its own and each key's.</summary>
    internal @this(int code, global::app.type.list.@this types) : base(types[typeof(@this)])
    {
        var type = types[typeof(modifier.@this)];
        _held = new (string Name, int Code)[] { ("alt", Alt), ("ctrl", Ctrl), ("meta", Meta), ("shift", Shift) }
            .Where(k => (code & k.Code) != 0).Select(k => new modifier.@this(k.Name, k.Code, type)).ToArray();
    }

    /// <summary>Its code on the screen's wire: each key held, counted once.</summary>
    internal int Code => _held.Aggregate(0, (code, modifier) => code | modifier.Code);

    /// <summary>True when <paramref name="name"/> is held.</summary>
    internal bool Holds(string name) => _held.Any(m => m.ToString() == name);

    /// <summary>Every key held, in the wire's order — a list of modifiers made by the asker's types.</summary>
    [global::app.LlmBuilder]
    public global::app.type.item.list.@this<modifier.@this> list(global::app.actor.context.@this context)
    {
        var held = new global::app.type.item.list.@this<modifier.@this>(
            context.App.type.list[typeof(global::app.type.item.list.@this<modifier.@this>)]);
        foreach (var modifier in _held)
            held.Add(new global::app.data.@this(modifier.ToString(), modifier, context: context));
        return held;
    }

    /// <summary>Read alone, the first key held.</summary>
    internal override bool IsFinal => false;

    /// <summary>The first key held; the null value when none is.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
        => new(_held.Length == 0 ? global::app.type.item.@null.@this.Instance : _held[0]);

    /// <summary><c>.list</c> is every key held; any other step walks the first.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => string.Equals(key, nameof(list), System.StringComparison.OrdinalIgnoreCase) && parent.Context is { } context
            ? new global::app.data.@this(key, list(context), parent: parent)
            : await base.Get(parent, key);

    /// <summary>The line's form: its code.</summary>
    public override void Write(global::app.type.format.IWriter writer) => writer.Int(Code);

    /// <summary>Shown, it is the whole list, by name.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        writer.BeginArray(_held.Length);
        foreach (var modifier in _held) modifier.Write(writer);
        writer.EndArray();
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    public override string ToString() => string.Join(",", _held.Select(m => m.ToString()));
}
