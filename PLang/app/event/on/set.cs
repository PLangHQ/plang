namespace app.@event.on;

/// <summary>A set: what runs before and after a variable is set.</summary>
public sealed class set : global::app.@event.@this
{
    internal set(binding.list.@this before, binding.list.@this after) : base("set", before, after) { }
}
