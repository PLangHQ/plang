namespace app.@event.on;

/// <summary>A start: what runs before and after something starts — a goal, a step, an action, the app.</summary>
public sealed class start : global::app.@event.@this
{
    internal start(binding.list.before before, binding.list.after after) : base("start", before, after) { }

    protected override global::app.@event.@this Of(global::app.@event.on.@this on) => on.start;
}
