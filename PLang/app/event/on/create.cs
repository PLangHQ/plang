namespace app.@event.on;

/// <summary>A create: what runs before and after a value is born through its type.</summary>
public sealed class create : global::app.@event.@this
{
    internal create(@this on, binding.list.before before, binding.list.after after) : base(on, "create", before, after) { }
}
