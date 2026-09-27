namespace app.@event.on;

/// <summary>An ask: what runs before and after a channel asks — after it, handed the answer.</summary>
public sealed class ask : global::app.@event.@this
{
    internal ask(binding.list.before before, binding.list.after after) : base("ask", before, after) { }

    protected override global::app.@event.@this Of(global::app.@event.on.@this on) => on.ask;
}
