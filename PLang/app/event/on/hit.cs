namespace app.@event.on;

/// <summary>A hit: what runs when a cache answers with what it holds.</summary>
public sealed class hit : global::app.@event.@this
{
    internal hit(binding.list.@this before, binding.list.@this after) : base("hit", before, after) { }
}
