namespace app.@event.on;

/// <summary>A write: what runs before and after a channel writes.</summary>
public sealed class write : global::app.@event.@this
{
    internal write(binding.list.@this before, binding.list.@this after) : base("write", before, after) { }
}
