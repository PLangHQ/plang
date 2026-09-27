namespace app.@event.on;

/// <summary>A load: what runs before and after a goal or a step is loaded.</summary>
public sealed class load : global::app.@event.@this
{
    internal load(binding.list.before before, binding.list.after after) : base("load", before, after) { }
}
