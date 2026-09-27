namespace app.@event.on;

/// <summary>An error: what runs when the thing it is on fails.</summary>
public sealed class error : global::app.@event.@this
{
    internal error(binding.list.@this before, binding.list.@this after) : base("error", before, after) { }
}
