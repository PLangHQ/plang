namespace app.@event.on;

/// <summary>A create: what runs before and after a value is born through its type.</summary>
public sealed class create : global::app.@event.@this
{
    internal create(binding.list.before before, binding.list.after after) : base("create", before, after) { }

    protected override global::app.@event.@this Of(global::app.@event.on.@this on) => on.create;
}
