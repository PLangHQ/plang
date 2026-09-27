namespace app.@event.on;

/// <summary>An ask: what runs before and after a channel asks.</summary>
public sealed class ask : global::app.@event.@this
{
    internal ask(binding.list.before before, binding.list.after after) : base("ask", before, after) { }
}
