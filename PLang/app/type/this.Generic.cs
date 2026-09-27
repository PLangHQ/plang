namespace app.type;

/// <summary>
/// The type of a collected concept — <c>app.goal</c>, <c>app.type</c>, <c>app.module</c>: the type
/// named X (goal, type, module), defined by X's class, over the list that holds the X's. One generic
/// class serves every concept. Its members: <see cref="list"/> (the X's), <see cref="Get(string)"/>
/// (the one by key), <see cref="current"/> (the one in play for the asker).
/// </summary>
public sealed class @this<T> : @this
    where T : item.@this, item.ICreate<T>, item.IMatch<T>, item.ICurrent<T>, item.IList<T>
{
    public @this(global::app.@this app) : base(item.@this.NameOf(typeof(T)), typeof(T)) => list = T.List(app);

    /// <summary>The X's loaded so far.</summary>
    public item.list.@this<T> list { get; }

    /// <summary>
    /// The one <paramref name="key"/> names — the first of <see cref="list"/> whose own
    /// <c>Match(key)</c> answers (a goal by its address, a type by its name or alias). No match is a
    /// NotFound result. C#'s door; plang's <c>["key"]</c> and <c>.key</c> reach it through navigation.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<data.@this<T>> Get(string key)
    {
        foreach (var p in list.Items())
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
        if (await new clr.@this(this, parent.Context).Get(parent, key) is { Success: true } member) return member;
        if (await base.Get(parent, key) is { Success: true } fact) return fact;
        var found = await Get(key);
        if (!found.Success) return found;
        return new data.@this(key, (await found.Value())!, parent: parent);
    }
}
