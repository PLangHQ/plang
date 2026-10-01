namespace app.type.item.variable.code;

/// <summary>
/// A position or key in brackets: <c>[0]</c> and <c>["k"]</c> are literals (a number, a text); a
/// bare path (<c>[idx]</c>, <c>[askInfo.gui]</c>) or <c>[%i%]</c> is a variable with its own code,
/// resolved at run. The container is handed the key's text form.
/// </summary>
public sealed class Index : Hop
{
    /// <summary>The key: a number, a text, or a variable.</summary>
    public global::app.type.item.@this Key { get; }

    internal Index(string text, global::app.type.item.@this key) : base(text) => Key = key;

    public override string Kind => "index";

    /// <summary>A literal key holds no variable; a variable key is own when it is.</summary>
    internal override bool IsOwn => Key.Variable.All(v => v.IsOwn);

    /// <summary><c>{"number": 0}</c>, <c>{"text": "k"}</c>, or <c>{"variable": [the key]}</c>.</summary>
    protected override void Piece(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        if (Key is variable.@this named)
        {
            writer.Name("variable");
            new variable.serializer.Entry().Write(writer, [named]);
        }
        else
        {
            writer.Name(Key.Type.Name);
            Key.Write(writer);
        }
        writer.EndObject();
    }

    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Start(
        global::app.data.@this? previous, global::app.actor.context.@this context)
    {
        if (previous is null) return context.NotFound(Text);
        var key = await Resolve(context);
        if (!key.Success) return key;
        return await previous.Peek().Get(previous, (await key.Value()).ToString()!, isIndex: true);
    }

    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Set(
        global::app.data.@this? parent, object? value, global::app.actor.context.@this context)
    {
        if (parent is null) return context.NotFound(Text);
        var key = await Resolve(context);
        if (!key.Success) return key;
        return await parent.Set((await key.Value()).ToString()!, isIndex: true, value);
    }

    // The key's value: a literal is itself; a variable key is what it holds — an unset one is the
    // program's error (IndexNotSet), never read as the literal text of its name.
    private async System.Threading.Tasks.ValueTask<global::app.data.@this> Resolve(global::app.actor.context.@this context)
    {
        if (Key is not variable.@this named) return context.Ok(Key);
        var held = await named.Start(context);
        if (held.IsInitialized && held.Peek() is not global::app.type.item.@null.@this) return held;

        var root = named.Code.Root;
        var rootData = await context.Variable.Get(root.Name);
        var diagnosis = rootData.IsInitialized
            ? $"%{root.Name}% holds {rootData.Type?.Name ?? "?"} = '{rootData.Peek().ToString()?.Split('\n')[0]}'"
            : $"%{root.Name}% is unset";
        return context.Error(new global::app.error.Error(
            $"cannot navigate {Text}: the index {named.Text} is not set — {diagnosis}", "IndexNotSet", 400));
    }
}
