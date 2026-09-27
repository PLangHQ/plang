namespace app.@event.on;

/// <summary>A read: what runs before and after a channel reads.</summary>
public sealed class read : global::app.@event.@this
{
    internal read(binding.list.before before, binding.list.after after) : base("read", before, after) { }
}
