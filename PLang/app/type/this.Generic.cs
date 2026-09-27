namespace app.type;

/// <summary>
/// The type of a collected concept — <c>app.goal</c>, <c>app.type</c>, <c>app.module</c>: the type
/// named X (goal, type, module), defined by X's class, over the list that holds the X's. One generic
/// class serves every concept. Its members: <see cref="list"/> (the X's), <see cref="Get(string)"/>
/// (the one by key), <see cref="current"/> (the one in play for the asker).
/// </summary>
public sealed class @this<T, L> : @this
    where T : item.@this, item.ICreate<T>, item.IMatch<T>, item.ICurrent<T>, item.IList<T, L>
    where L : item.list.@this<T>
{
    public @this(global::app.@this app) : base(item.@this.NameOf(typeof(T)), typeof(T)) => list = T.List(app);

    /// <summary>The X's loaded so far — the list class T names, with its own work.</summary>
    public L list { get; }

    /// <summary>
    /// A collected type's face in the Out view is a summary — the names of its list (a type plang
    /// keeps for itself stays out); detail comes by navigating to one. Every other view writes the
    /// type's identity.
    /// </summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.channel.serializer.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        if (mode != global::app.View.Out) return base.Output(writer, mode, context);
        var names = list.Items().Where(p => p is not @this { Internal: true }).Select(p => p.ToString()!).ToList();
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
    public async System.Threading.Tasks.ValueTask<data.@this<T>> Get(string key)
    {
        // walked as the list reaches its items — a list that reads them (goal's) stops at the match
        await foreach (var p in list.Every())
            if (await p.Match(key) is { } found) return data.@this<T>.Ok(found);
        return data.@this<T>.FromError(new global::app.error.Error($"no {Name} '{key}'", "NotFound", 404));
    }

    /// <summary>The one in play for the asker — the running goal, the acting actor; NotFound where
    /// nothing is inside one. Its Data is born with the asker's context.</summary>
    public data.@this<T> current(actor.context.@this context)
        => T.Current(context) is { } one ? new data.@this<T>("current", one, context: context)
            : data.@this<T>.FromError(new global::app.error.Error($"no {Name} is current", "NotFound", 404));

    /// <summary>
    /// plang's door, one navigation step: the type's own members first (<c>current</c> with the
    /// asker's context, then <c>list</c> and the facts), and a key that is none of them is one X —
    /// <see cref="Get(string)"/>. The answer takes its context from the parent.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<data.@this> Get(data.@this parent, string key)
    {
        if (string.Equals(key, "current", System.StringComparison.OrdinalIgnoreCase))
            return current(parent.Context);
        // a miss is NotFound — a Data that holds nothing (not initialized), so the next door asks
        if (await new clr.@this(this, parent.Context).Get(parent, key) is { Success: true, IsInitialized: true } member) return member;
        if (await base.Get(parent, key) is { Success: true, IsInitialized: true } fact) return fact;
        var found = await Get(key);
        if (!found.Success) return found;
        return new data.@this(key, (await found.Value())!, parent: parent);
    }
}
