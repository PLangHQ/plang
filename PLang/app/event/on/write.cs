namespace app.@event.on;

/// <summary>A write: what runs before and after a channel writes.</summary>
public sealed class write : global::app.@event.@this
{
    internal write(binding.list.before before, binding.list.after after) : base("write", before, after) { }
}
