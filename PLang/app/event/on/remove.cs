namespace app.@event.on;

/// <summary>A remove: what runs before and after a variable is removed.</summary>
public sealed class remove : global::app.@event.@this
{
    internal remove(binding.list.before before, binding.list.after after) : base("remove", before, after) { }
}
