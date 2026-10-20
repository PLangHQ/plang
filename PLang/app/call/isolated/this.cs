namespace app.call.isolated;

/// <summary>
/// A frame with memory of its own: every write made under it stays in it and ends with it — nothing reaches its
/// caller (an LLM tool's goal, a shortcut's, a task's run). Reads still fall through to its caller's frames and
/// memory.
/// </summary>
public sealed class @this : binding.@this
{
    internal @this(global::app.type.@this type, call.@this? caller, list.@this stack, global::app.type.@this datetime,
        global::app.type.@this dict,
        IEnumerable<global::app.data.@this>? names, global::app.goal.step.action.@this? held)
        : base(type, caller, stack, datetime, dict, names, held) { }

    /// <summary>Every write lands here.</summary>
    internal override call.@this Keeper(string name) => this;
}
