namespace app.type;

/// <summary>
/// The type of a collected concept — <c>app.type</c>, <c>app.variable</c>: the type named X, defined by X's
/// class, over the list that holds the X's. Its members: <see cref="list"/> (the X's) and
/// <see cref="Get(string)"/> (the one by key). The node is its type — the type list's entry of its name — so a dot
/// reads its own members, then the type's facts (<c>name</c>, <c>on</c>), then one X by name, for a concept nothing is
/// ever inside. Brackets always pick one by name (<c>%!app.type["goal"]%</c>). A concept execution is inside has a
/// node with a current (<see cref="current.@this{T, L}"/>), where the dot's last step reads the current's member.
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

    /// <summary>The concept's type, its facts read from its class through the app's types — so it can
    /// stand as the type list's entry of its name. <c>app.type</c> itself is born before there is a list
    /// to read through, and is its identity alone.</summary>
    public @this(global::app.@this app) : base(item.@this.NameOf(typeof(T)), typeof(T), app.type?.list)
    {
        _app = app;
        _list = new(() => T.List(_app), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        _setting = typeof(T).GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(item.setting.IConcept<>))
            ?.GetGenericArguments()[0];
    }

    /// <summary>The X's loaded so far — the list class T names, with its own work. Made on first
    /// read; a concept whose list belongs to the asker has none here (its <c>List</c> says so).</summary>
    public L list => _list.Value;

    // The list the asker sees — its own for a concept whose list belongs to the asker, else the app's.
    // Navigation always asks through here: one path for every concept.
    private protected L Of(actor.context.@this context) => T.Of(context) ?? list;

    /// <summary>
    /// A collected type's face in the Out view is a summary — the names of its list (a type plang
    /// keeps for itself stays out); detail comes by navigating to one. Every other view writes the
    /// type's identity.
    /// </summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        if (mode != global::app.View.Out) return base.Output(writer, mode, context);
        var names = (context != null ? Of(context) : list).Items().Where(p => p is not @this { Internal: true }).Select(p => p.ToString()!).ToList();
        writer.BeginObject();
        writer.Name("list");
        writer.BeginArray(names.Count);
        foreach (var name in names) writer.String(name);
        writer.EndArray();
        writer.EndObject();
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    /// <summary>
    /// The one <paramref name="key"/> names — the first of <see cref="list"/> whose own
    /// <c>Match(key)</c> answers (a goal by its address, a type by its name or alias). No match is a
    /// NotFound result. C#'s door; plang's <c>["key"]</c> and <c>.key</c> reach it through navigation.
    /// </summary>
    public System.Threading.Tasks.ValueTask<data.@this<T>> Get(string key) => Find(list.Walk(), key);

    // The one key names among the items given — walked as the list reaches them, so a list that
    // reads them (goal's) stops at the match.
    private async System.Threading.Tasks.ValueTask<data.@this<T>> Find(System.Collections.Generic.IAsyncEnumerable<T> every, string key)
    {
        await foreach (var p in every)
            if (await p.Match(key) is { } found) return data.@this<T>.Ok(found);
        return data.@this<T>.FromError(new global::app.error.Error($"no {Name} '{key}'", "NotFound", 404));
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
        => await Own(parent, key) ?? await Fact(parent, key) ?? await Named(parent, key);

    /// <summary>Brackets pick one X by name, on every node (<c>%!app.actor["user"]%</c>, <c>%!goal["Show"]%</c>).</summary>
    public override async System.Threading.Tasks.ValueTask<data.@this> Get(data.@this parent, string key, bool isIndex)
        => isIndex ? await Named(parent, key) : await Get(parent, key);

    /// <summary>The collection's own members for <paramref name="key"/> — <c>list</c> as the asker sees it, the
    /// concept's <c>setting</c>; null when the key is neither.</summary>
    private protected async System.Threading.Tasks.ValueTask<data.@this?> Own(data.@this parent, string key)
    {
        if (string.Equals(key, "list", System.StringComparison.OrdinalIgnoreCase))
            return new data.@this(key, Of(parent.Context), parent: parent);
        // the concept's own settings (%!app.test.setting%), as the asker's settings build them
        if (_setting is { } @class && string.Equals(key, "setting", System.StringComparison.OrdinalIgnoreCase))
            return await parent.Context.Setting.Of(@class);
        return null;
    }

    /// <summary>The type's own facts for <paramref name="key"/> — its name, its events (<c>on</c>), its kind; null when
    /// the key is none of them. The node and its type are one object, and the type's facts answer before the
    /// concept's elements (<c>%!goal.Name%</c> is <c>goal</c>).</summary>
    private protected async System.Threading.Tasks.ValueTask<data.@this?> Fact(data.@this parent, string key)
    {
        // a miss is NotFound — a Data that holds nothing (not initialized), so the next door asks
        if (await new clr.@this(this, parent.Context).Get(parent, key) is { Success: true, IsInitialized: true } member) return member;
        if (await base.Get(parent, key) is { Success: true, IsInitialized: true } fact) return fact;
        return null;
    }

    // One X by name: selected the way its slots are (a goal from the calling goal), else found in the list the
    // asker sees.
    private async System.Threading.Tasks.ValueTask<data.@this> Named(data.@this parent, string key)
    {
        if (await T.Select(new global::app.type.item.text.@this(key), parent.Context) is { IsInitialized: true } selected)
            return selected.Success ? new data.@this(key, selected.Peek(), parent: parent) : selected;
        var found = await Find(Of(parent.Context).Walk(null, parent.Context), key);
        if (!found.Success) return found;
        return new data.@this(key, (await found.Value())!, parent: parent);
    }
}
