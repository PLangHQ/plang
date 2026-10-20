namespace app.type.item.variable.code;

/// <summary>
/// The root: the name the value lives under in memory (<c>user</c>, <c>!app</c>, <c>!data</c>).
/// </summary>
public sealed class Variable : Hop
{
    public string Name { get; }

    internal Variable(string name, Hop? next, global::app.type.@this type) : base(name, next, type) => Name = name;

    public override string Kind => "variable";

    /// <summary>A name the program sets is its own; a <c>!</c> name (<c>%!app%</c>, <c>%!trace%</c>) is the
    /// app's or a binding's.</summary>
    internal override bool IsOwn => !Name.StartsWith('!');

    protected override System.Threading.Tasks.ValueTask Piece(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context) { writer.String(Name); return default; }

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
        if (await context.App.shortcut.Get(name, context) is { Success: true } shortcut && await shortcut.Value() is { } held)
            return await held.Read(context);
        var app = await context.Variable.Get("!app");
        var member = await app.Peek().Get(app, name);
        if (member.IsInitialized && !member.Peek().IsNull) return member;
        var module = await context.App.module.Get(name, context);
        return module.Success ? new global::app.data.@this(name, await module.Value(), context: context) : context.NotFound(name);
    }

    /// <summary>Whether the name names something a program can read — a name the program sets is its own, always; a
    /// <c>!</c> name is one a run binds (an action's answer <c>%!data%</c>, the event in play <c>%!event%</c>), else what
    /// <see cref="Start"/> would read: a binding, a shortcut, the app's member, a module. <c>%!photo.png%</c> names
    /// none of them.</summary>
    internal async System.Threading.Tasks.ValueTask<bool> Names(global::app.actor.context.@this context)
        => IsOwn || Bound.Contains(Name, global::app.type.item.variable.list.@this.Comparer)
           || (await Start(null, context)).IsInitialized;

    // the ! names a run binds as it goes — never there before it runs
    private static readonly string[] Bound = ["!data", "!event", "!buildData"];

    /// <summary>What the name holds, then the steps after it — the first of them stepping on the variable's own
    /// binding.</summary>
    internal override async System.Threading.Tasks.ValueTask<global::app.data.@this> Read(
        global::app.data.@this? previous, global::app.actor.context.@this context, bool own = false)
    {
        var binding = await Start(null, context);
        return Next is null || !binding.Success || !binding.IsInitialized ? binding : await Next.Read(binding, context, own: true);
    }

    /// <summary>The variable rebinds to <paramref name="value"/>; with steps after it, the next writes on the binding it
    /// says it writes on.</summary>
    internal override async System.Threading.Tasks.ValueTask<global::app.data.@this> Write(
        global::app.data.@this? parent, object? value, global::app.actor.context.@this context, bool own = false)
        => Next is null
            ? await Set(null, value, context)
            : await Next.Write(await Next.Binding(this, context), value, context, own: true);

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
