namespace app.type.node.current;

/// <summary>
/// The app's node for a concept execution is inside — <c>app.goal</c>, <c>app.actor</c>, <c>app.test</c>,
/// <c>app.error</c>: one of its kind is in play for the asker (<see cref="current"/>). A dot past the node's own members
/// reads the current's member (<c>%!app.goal.name%</c> the running goal's, <c>%!error.message%</c> the error in play's);
/// brackets pick one by name (<c>%!app.actor["user"]%</c>). Written alone, it is the one in play.
/// </summary>
public sealed class @this<T, L> : global::app.type.node.@this<T, L>
    where T : item.@this, item.ICreate<T>, item.IMatch<T>, item.ICurrent<T>, item.ILoad<T>, item.IList<T, L>
    where L : item.list.@this<T>
{
    public @this(global::app.@this app) : base(app) { }

    /// <summary>The one in play for the asker — the running goal, the acting actor; NotFound where
    /// nothing is inside one. Its Data is born with the asker's context.</summary>
    public data.@this<T> current(actor.context.@this context)
        => T.Current(context) is { } one ? new data.@this<T>("current", one, context: context)
            : data.@this<T>.FromError(new global::app.error.@this($"no {type.Name} is current", "NotFound", 404), context);

    /// <summary>Written alone, the node is the one in play for the writer's asker (<c>%!app.goal%</c> the running goal);
    /// with nothing in play, nothing.</summary>
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        if (T.Current(writer.Context) is { } one) return one.Output(writer);
        writer.Null();
        return default;
    }

    /// <summary>One step by dot: <c>current</c>, the node's own members, then the current's member.</summary>
    public override async System.Threading.Tasks.ValueTask<data.@this> Get(data.@this parent, string key)
    {
        if (string.Equals(key, "current", System.StringComparison.OrdinalIgnoreCase))
            return current(parent.Context);
        if (await Own(parent, key) is { } own) return own;
        // with nothing inside one, a member of it is unset, as any miss is
        var now = current(parent.Context);
        return now.Success ? await now.Peek().Get(now, key) : parent.Context.NotFound(key);
    }
}
