namespace app.@event.on;

/// <summary>A write: what runs before and after a channel writes — before it, handed the data about to be
/// written; after it, the write's result.</summary>
public sealed class write : global::app.@event.@this
{
    internal write(@this on, binding.list.before before, binding.list.after after) : base(on, "write", before, after) { }
}
