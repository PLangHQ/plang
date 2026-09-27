namespace app.@event.on;

/// <summary>A remove: what runs before and after a variable is removed.</summary>
public sealed class remove : global::app.@event.@this
{
    internal remove(binding.list.@this before, binding.list.@this after) : base("remove", before, after) { }
}
