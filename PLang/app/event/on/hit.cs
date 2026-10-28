namespace app.@event.on;

/// <summary>A hit: what runs when a cache answers with what it holds.</summary>
public sealed class hit : global::app.@event.@this
{
    internal hit(@this on, binding.list.before before, binding.list.after after) : base(on, "hit", before, after) { }
}
