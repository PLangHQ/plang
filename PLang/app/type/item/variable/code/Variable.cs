namespace app.type.item.variable.code;

/// <summary>
/// The root: the name the value lives under in memory (<c>user</c>, <c>!app</c>, <c>!data</c>).
/// </summary>
public sealed class Variable : Hop
{
    public string Name { get; }

    internal Variable(string name) : base(name) => Name = name;

    public override string Kind => "variable";

    protected override void Piece(global::app.type.format.IWriter writer) => writer.String(Name);

    /// <summary>What the name holds. A <c>!</c> name the memory doesn't bind is the app's shortcut by that name
    /// (<c>%!goal%</c>: what its goal answers for this asker), else the app's member (<c>%!build%</c> while
    /// building), else the module by that name (<c>%!llm%</c>) — a member holding nothing is not the app's. The
    /// bindings (<c>!app</c>, <c>!data</c>, …) answer first.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Start(
        global::app.data.@this? previous, global::app.actor.context.@this context)
    {
        var bound = await context.Variable.Get(Name);
        if (bound.IsInitialized || !Name.StartsWith('!')) return bound;
        var name = Name[1..];
        if (await context.App.shortcut.Get(name) is { Success: true } shortcut && await shortcut.Value() is { } held)
            return await held.Read(context);
        var app = await context.Variable.Get("!app");
        var member = await app.Peek().Get(app, name);
        if (member.IsInitialized && !member.Peek().IsNull) return member;
        var module = await context.App.module.Get(name);
        return module.Success ? new global::app.data.@this(name, await module.Value(), context: context) : context.NotFound(name);
    }

    /// <summary>The variable rebinds to <paramref name="value"/>.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Set(
        global::app.data.@this? parent, object? value, global::app.actor.context.@this context)
        => await context.Variable.Set(Name, value);

    /// <summary>What the name holds, or — when it holds nothing — an empty dict stored under it,
    /// so a write deeper in (<c>set %x.a% = 1</c> on a new <c>%x%</c>) has a place to land. A <c>!</c>
    /// name the memory doesn't bind that is the app's or a module (<c>%!http.request.setting.timeout%</c>) is
    /// that, which takes the write itself.</summary>
    internal async System.Threading.Tasks.ValueTask<global::app.data.@this> Ensure(global::app.actor.context.@this context)
    {
        if (Name.StartsWith('!') && await Start(null, context) is { IsInitialized: true } held) return held;
        return await context.Variable.Ensure(Name,
            () => context.App.type.list["dict"].Create(new System.Collections.Generic.Dictionary<string, object?>(), context));
    }
}
