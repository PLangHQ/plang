namespace app.type.node;

/// <summary>What every node is — the type <c>node</c>, plang's own: no step names it as a type. A node takes part in a
/// snapshot as what holds its concept's parts (<c>app.actor</c>'s list), with nothing of its own; a node whose list is
/// no part leaves nothing.</summary>
[global::app.Attributes.PlangType("node")]
public abstract class @this : global::app.type.item.@this, global::app.snapshot.ISnapshot
{
    public static bool Internal => true;

    private protected @this(global::app.type.@this type) : base(type) { }
}

/// <summary>
/// The app's node for a collected concept — <c>app.variable</c>, <c>app.shortcut</c>: the concept's
/// <see cref="type"/> (the type list's own entry for its class), its <see cref="list"/> (the X's) and
/// <see cref="Get(string, actor.context.@this)"/> (the one by key). Not a type: <c>%!app.goal.type%</c> is the type,
/// and the node is what the app holds for the concept. A dot reads the node's own members, then one X by name;
/// brackets always pick one by name (<c>%!app.actor["user"]%</c>). Written alone, it is its list as the asker sees it.
/// A concept execution is inside has a node with a current (<see cref="current.@this{T, L}"/>).
/// </summary>
public class @this<T, L> : @this
    where T : item.@this, item.ICreate<T>, item.IMatch<T>, item.ILoad<T>, item.IList<T, L>
    where L : item.list.@this<T>
{
    // The app the concept is born with — used only for the app's own work (its list, reading its own files
    // as the system), never in place of an asker's context.
    private readonly global::app.@this _app;
    // the list, once made, and the gate that makes it once
    private L? _list;
    private bool _made;
    private object? _gate;

    /// <summary>The app's node for <typeparamref name="T"/>, its list made from the class on first read.</summary>
    public @this(global::app.@this app) : base(app.type.list[typeof(global::app.type.node.@this)]) => _app = app;

    /// <summary>A node of the type <paramref name="node"/>, born holding its list <paramref name="list"/> — the types' own
    /// node, born before the app holds them.</summary>
    private protected @this(global::app.@this app, global::app.type.@this node, L list) : base(node)
    {
        _app = app;
        _list = list;
        _made = true;
    }

    /// <summary>The concept's type — the app's types' entry for its class (<c>%!app.goal.type.name%</c> is <c>goal</c>):
    /// its facts, its events (<c>.on</c>). Found when first asked: the types answer the same entry every time.</summary>
    public global::app.type.@this type => _type ??= _app.type.list[typeof(T)];
    private global::app.type.@this? _type;

    /// <summary>The X's loaded so far — the list class T names, with its own work. Made on first
    /// read, once; a concept whose list belongs to the asker has none here (its <c>List</c> says so).</summary>
    public L list => System.Threading.Volatile.Read(ref _made) ? _list!
        : System.Threading.LazyInitializer.EnsureInitialized(ref _list, ref _made, ref _gate, () => T.List(_app))!;

    // The list the asker sees — its own for a concept whose list belongs to the asker, else the app's.
    // Navigation always asks through here: one path for every concept.
    private protected L Of(actor.context.@this context) => T.Of(context) ?? list;

    /// <summary>Written alone, the node is its list as the writer's asker sees it (<c>%!app.variable%</c> the asker's
    /// variables).</summary>
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
        => Of(writer.Context).Output(writer);

    /// <summary>
    /// The one <paramref name="key"/> names — the first of <see cref="list"/> whose own
    /// <c>Match(key)</c> answers (a goal by its address, a type by its name or alias). No match is a
    /// NotFound result. C#'s door; plang's <c>["key"]</c> and <c>.key</c> reach it through navigation.
    /// </summary>
    public System.Threading.Tasks.ValueTask<data.@this<T>> Get(string key, actor.context.@this context)
        => Find(list.Walk(), key, context);

    // The one key names among the items given — walked as the list reaches them, so a list that
    // reads them (goal's) stops at the match. The answer is born with the asker's context.
    private async System.Threading.Tasks.ValueTask<data.@this<T>> Find(System.Collections.Generic.IAsyncEnumerable<T> every, string key,
        actor.context.@this context)
    {
        await foreach (var p in every)
            if (await p.Match(key) is { } element) return context.Ok<T>(element);
        return data.@this<T>.FromError(new global::app.error.@this($"no {type.Name} '{key}'", "NotFound", 404), context);
    }

    /// <summary>The one <paramref name="location"/> holds (<c>app.goal.Load("/system/error/Show.goal")</c>),
    /// resolved and read as the app itself; the element says how it loads.</summary>
    public System.Threading.Tasks.Task<data.@this> Load(string location)
        => T.Load(item.path.@this.Resolve(location, _app.actor.list.System.Context), _app);

    /// <summary>
    /// plang's door, one navigation step by dot: the node's own members, then one X by name.
    /// The answer takes its context from the parent.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<data.@this> Get(data.@this parent, string key)
        => await Own(parent, key) ?? await Named(parent, key);

    /// <summary>Brackets pick one X by name, on every node (<c>%!app.actor["user"]%</c>, <c>%!goal["Show"]%</c>).</summary>
    public override async System.Threading.Tasks.ValueTask<data.@this> Get(data.@this parent, string key, bool isIndex)
        => isIndex ? await Named(parent, key) : await Get(parent, key);

    /// <summary>The node's own members for <paramref name="key"/> — <c>list</c> as the asker sees it, the concept's
    /// <c>setting</c>, its <c>type</c>; null when the key is none of them.</summary>
    private protected async System.Threading.Tasks.ValueTask<data.@this?> Own(data.@this parent, string key)
    {
        if (string.Equals(key, "list", System.StringComparison.OrdinalIgnoreCase))
            return new data.@this(key, Of(parent.Context), parent: parent);
        // the concept's own settings (%!app.test.setting%), as the asker's settings build them: the class its element
        // names (test: IConcept<test.setting>), a fact of the class as the code that made the concept's type read it
        if (string.Equals(key, "setting", System.StringComparison.OrdinalIgnoreCase)
            && type.Facts(typeof(T)).Setting is { } @class)
            return await parent.Context.Setting.Get(@class) ?? parent.Context.NotFound(key);
        if (string.Equals(key, "type", System.StringComparison.OrdinalIgnoreCase))
            return new data.@this(key, type, parent: parent);
        return null;
    }

    // One X by name: selected the way its slots are (a goal from the calling goal), else found in the list the
    // asker sees.
    private async System.Threading.Tasks.ValueTask<data.@this> Named(data.@this parent, string key)
    {
        if (await T.Select(new global::app.type.item.text.@this(key, _app.type.list[typeof(global::app.type.item.text.@this)]), parent.Context)
            is { IsInitialized: true } selected)
            return selected.Success ? new data.@this(key, selected.Peek(), parent: parent) : selected;
        var element = await Find(Of(parent.Context).Walk(null, parent.Context), key, parent.Context);
        if (!element.Success) return element;
        return new data.@this(key, (await element.Value())!, parent: parent);
    }
}
