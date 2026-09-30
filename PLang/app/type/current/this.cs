namespace app.type.current;

/// <summary>
/// The type of a concept execution is inside — <c>app.goal</c>, <c>app.actor</c>, <c>app.test</c>, <c>app.error</c>:
/// one of its kind is in play for the asker (<see cref="current"/>). A dot past the node's own members and the type's
/// facts reads the current's member (<c>%!goal.Name%</c> is the type's name, <c>goal</c>; <c>%!goal.current.Name%</c>
/// the running goal's; <c>%!error.Message%</c> the error in play's); brackets pick one by name
/// (<c>%!app.actor["user"]%</c>).
/// </summary>
public sealed class @this<T, L> : global::app.type.@this<T, L>
    where T : item.@this, item.ICreate<T>, item.IMatch<T>, item.ICurrent<T>, item.ILoad<T>, item.IList<T, L>
    where L : item.list.@this<T>
{
    public @this(global::app.@this app) : base(app) { }

    /// <summary>The one in play for the asker — the running goal, the acting actor; NotFound where
    /// nothing is inside one. Its Data is born with the asker's context.</summary>
    public data.@this<T> current(actor.context.@this context)
        => T.Current(context) is { } one ? new data.@this<T>("current", one, context: context)
            : data.@this<T>.FromError(new global::app.error.Error($"no {Name} is current", "NotFound", 404));

    /// <summary>One step by dot: <c>current</c>, the node's own members, the type's facts, then the current's member.</summary>
    public override async System.Threading.Tasks.ValueTask<data.@this> Get(data.@this parent, string key)
    {
        if (string.Equals(key, "current", System.StringComparison.OrdinalIgnoreCase))
            return current(parent.Context);
        if (await Own(parent, key) is { } own) return own;
        if (await Fact(parent, key) is { } fact) return fact;
        // with nothing inside one, a member of it is unset, as any miss is
        var now = current(parent.Context);
        return now.Success ? await now.Peek().Get(now, key) : parent.Context.NotFound(key);
    }
}
