namespace app.type.item.variable.call.isolated;

/// <summary>
/// A call with memory of its own: every write made under it stays in it and ends with it — nothing reaches
/// the caller (an LLM tool's goal, until a service gives it its own actor). Reads still see the caller.
/// </summary>
public sealed class @this : call.@this
{
    internal @this(IEnumerable<data.@this>? parameters, call.@this? caller, call.list.@this owner,
        global::app.goal.step.action.@this? held = null)
        : base(parameters, caller, owner, held) { }

    /// <summary>Every write lands here.</summary>
    internal override call.@this Keeper(string name) => this;
}
