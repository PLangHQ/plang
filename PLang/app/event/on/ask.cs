namespace app.@event.on;

/// <summary>An ask: what runs before and after a channel asks.</summary>
public sealed class ask : global::app.@event.@this
{
    internal ask(binding.list.@this before, binding.list.@this after) : base("ask", before, after) { }
}
