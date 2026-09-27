namespace app.@event.on;

/// <summary>A write: what runs before and after a channel writes — before it, handed the data about to be
/// written; after it, the write's result.</summary>
public sealed class write : global::app.@event.@this
{
    internal write(binding.list.before before, binding.list.after after) : base("write", before, after) { }

    protected override global::app.@event.@this Of(global::app.@event.on.@this on) => on.write;
}
