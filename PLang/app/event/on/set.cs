namespace app.@event.on;

/// <summary>A set: what runs before and after a variable is set — before it, handed the value about to be
/// stored; after it, the stored one.</summary>
public sealed class set : global::app.@event.@this
{
    internal set(binding.list.before before, binding.list.after after) : base("set", before, after) { }
}
