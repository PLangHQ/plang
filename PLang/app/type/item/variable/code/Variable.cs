namespace app.type.item.variable.code;

/// <summary>
/// The root: the name the value lives under in memory (<c>user</c>, <c>!app</c>, <c>!data</c>).
/// </summary>
public sealed class Variable : Hop
{
    public string Name { get; }

    internal Variable(string name) : base(name) => Name = name;

    public override string Kind => "variable";

    protected override void Piece(global::app.channel.serializer.IWriter writer) => writer.String(Name);

    /// <summary>What the name holds; a <c>!</c> name the memory doesn't bind (<c>%!goal.list.setting%</c>)
    /// is the asker's settings' — the bindings (<c>!app</c>, <c>!data</c>, …) answer first.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Start(
        global::app.data.@this? previous, global::app.actor.context.@this context)
    {
        var bound = await context.Variable.Get(Name);
        if (bound.IsInitialized || !Name.StartsWith('!')) return bound;
        return await context.Setting.Get(Name[1..]);
    }

    /// <summary>The variable rebinds to <paramref name="value"/>.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Set(
        global::app.data.@this? parent, object? value, global::app.actor.context.@this context)
        => await context.Variable.Set(Name, value);

    /// <summary>What the name holds, or — when it holds nothing — an empty dict stored under it,
    /// so a write deeper in (<c>set %x.a% = 1</c> on a new <c>%x%</c>) has a place to land.</summary>
    internal async System.Threading.Tasks.ValueTask<global::app.data.@this> Ensure(global::app.actor.context.@this context)
        => await context.Variable.Ensure(Name, () => new global::app.type.item.dict.@this());
}
