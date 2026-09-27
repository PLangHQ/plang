namespace app.@event.on;

/// <summary>A create: what runs before and after a value is born through its type.</summary>
public sealed class create : global::app.@event.@this
{
    internal create(binding.list.@this before, binding.list.@this after) : base("create", before, after) { }
}
