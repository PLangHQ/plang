namespace app.@event.on;

/// <summary>An ask: what runs before and after a channel asks — after it, handed the answer.</summary>
public sealed class ask : global::app.@event.@this
{
    internal ask(@this on, binding.list.before before, binding.list.after after) : base(on, "ask", before, after) { }
}
