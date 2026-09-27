namespace app.@event.on;

/// <summary>A read: what runs before and after a channel reads — after it, handed what was read.</summary>
public sealed class read : global::app.@event.@this
{
    internal read(binding.list.before before, binding.list.after after) : base("read", before, after) { }

    protected override global::app.@event.@this Of(global::app.@event.on.@this on) => on.read;
}
