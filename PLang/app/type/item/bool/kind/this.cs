namespace app.type.item.@bool.kind;

/// <summary>
/// The bool type's own kind — what a bool can be in a step, as the decider is offered it: true or false, a closed pair.
/// Offered, never a step's variable (<c>set default %flag% = true</c> is true, not %flag%). The pair is offers only: a
/// bool's values aren't a closed set of strings the formal reader checks a literal against.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    public @this() : base("") { }

    protected internal override string Owner => "bool";

    public override System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>> Offers(global::app.goal.step.@this step)
        => new([global::app.type.item.@bool.@this.True, global::app.type.item.@bool.@this.False]);

    /// <summary>A bool is true or false, nothing else.</summary>
    public override bool IsClosed => true;
}
