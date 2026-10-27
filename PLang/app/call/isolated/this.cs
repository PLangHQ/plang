namespace app.call.isolated;

/// <summary>
/// A frame with memory of its own: every write made under it stays in it and ends with it — nothing reaches its
/// caller (an LLM tool's goal, a shortcut's, a task's run). Reads still fall through to its caller's frames and
/// memory.
/// </summary>
public sealed class @this : binding.@this, global::app.type.item.ICreate<@this>
{
    internal @this(global::app.type.@this type, call.@this? caller, list.@this stack,
        IEnumerable<global::app.data.@this>? names, global::app.goal.step.action.@this? callee)
        : base(type, caller, stack, names, callee) { }

    /// <summary>An isolated frame come back from a snapshot: its stored <paramref name="node"/> read whole — every write
    /// made under it among its own variables — under <paramref name="stack"/>'s current frame.</summary>
    private @this(global::app.snapshot.@this node, global::app.type.@this type, list.@this stack, global::app.actor.context.@this context)
        : base(node, type, stack, context) { }

    /// <summary>The pure core — an isolated frame passes through; nothing else converts into one.</summary>
    public static new @this? Create(object? raw) => raw as @this;

    /// <summary>An isolated frame passes through; a stored one's node comes back as one, under the current frame of the
    /// stack <paramref name="data"/>'s context calls.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw switch
    {
        @this isolated => isolated,
        global::app.snapshot.@this node => new @this(node, type ?? data.Context.App.type.list[typeof(@this)], data.Context.call, data.Context),
        _ => null,
    };

    /// <summary>Every write lands here.</summary>
    internal override call.@this Keeper(string name) => this;
}
