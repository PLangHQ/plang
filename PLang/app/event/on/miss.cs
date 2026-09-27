namespace app.@event.on;

/// <summary>A miss: what runs when a cache holds nothing for the key.</summary>
public sealed class miss : global::app.@event.@this
{
    internal miss(binding.list.@this before, binding.list.@this after) : base("miss", before, after) { }
}
