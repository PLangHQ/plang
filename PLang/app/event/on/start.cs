namespace app.@event.on;

/// <summary>A start: what runs before and after something starts — a goal, a step, an action, the app.</summary>
public sealed class start : global::app.@event.@this
{
    internal start(binding.list.@this before, binding.list.@this after) : base("start", before, after) { }
}
