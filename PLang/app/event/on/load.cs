namespace app.@event.on;

/// <summary>A load: what runs before and after a goal is loaded — before it, handed the <c>.pr</c> it is read
/// from (there is no goal yet); after it, the goal.</summary>
public sealed class load : global::app.@event.@this
{
    internal load(binding.list.before before, binding.list.after after) : base("load", before, after) { }

    protected override global::app.@event.@this Of(global::app.@event.on.@this on) => on.load;
}
