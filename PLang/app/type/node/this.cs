namespace app.type.node;

/// <summary>What every node is — the type <c>node</c>, plang's own: no step names it as a type.</summary>
[global::app.Attributes.PlangType("node")]
public abstract class @this : global::app.type.item.@this
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
    private readonly System.Lazy<L> _list;
    // The class of the concept's own settings, when its element names one (test: IConcept<test.setting>).
    private readonly System.Type? _setting;

    /// <summary>The app's node for <typeparamref name="T"/>, its list made from the class on first read.</summary>
    public @this(global::app.@this app) : this(app, app.type.list, () => T.List(app)) { }

    /// <summary>A node over the types <paramref name="types"/> and the list <paramref name="list"/> makes — the
    /// types' own node is born before the app holds them.</summary>
    private protected @this(global::app.@this app, global::app.type.list.@this types, System.Func<L> list)
        : base(types[typeof(global::app.type.node.@this)])
    {
        _app = app;
        _list = new(list, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        _setting = typeof(T).GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(item.setting.IConcept<>))
            ?.GetGenericArguments()[0];
        type = types[typeof(T)];
    }

    /// <summary>The concept's type — the type list's entry for its class (<c>%!app.goal.type.name%</c> is <c>goal</c>):
    /// its facts, its events (<c>.on</c>).</summary>
    public global::app.type.@this type { get; }

    /// <summary>The X's loaded so far — the list class T names, with its own work. Made on first
    /// read; a concept whose list belongs to the asker has none here (its <c>List</c> says so).</summary>
    public L list => _list.Value;

    // The list the asker sees — its own for a concept whose list belongs to the asker, else the app's.
    // Navigation always asks through here: one path for every concept.
    private protected L Of(actor.context.@this context) => T.Of(context) ?? list;

    /// <summary>Written alone, the node is its list as the asker sees it (<c>%!app.variable%</c> the asker's variables);
    /// with no asker, the app's.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
        => (context != null ? Of(context) : list).Output(writer, mode, context);

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
            if (await p.Match(key) is { } found) return context.Ok<T>(found);
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
        // the concept's own settings (%!app.test.setting%), as the asker's settings build them
        if (_setting is { } @class && string.Equals(key, "setting", System.StringComparison.OrdinalIgnoreCase))
            return await parent.Context.Setting.Of(@class);
        if (string.Equals(key, "type", System.StringComparison.OrdinalIgnoreCase))
            return new data.@this(key, type, parent: parent);
        return null;
    }

    // One X by name: selected the way its slots are (a goal from the calling goal), else found in the list the
    // asker sees.
    private async System.Threading.Tasks.ValueTask<data.@this> Named(data.@this parent, string key)
    {
        if (await T.Select(new global::app.type.item.text.@this(key), parent.Context) is { IsInitialized: true } selected)
            return selected.Success ? new data.@this(key, selected.Peek(), parent: parent) : selected;
        var found = await Find(Of(parent.Context).Walk(null, parent.Context), key, parent.Context);
        if (!found.Success) return found;
        return new data.@this(key, (await found.Value())!, parent: parent);
    }
}
