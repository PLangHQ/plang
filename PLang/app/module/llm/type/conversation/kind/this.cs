namespace app.module.llm.type.conversation.kind;

/// <summary>
/// The conversation type's own kind — what a conversation can be in a step, as the decider is offered it. A
/// conversation continues an earlier query's answer, so it is offered the step's variables the build can't tell apart
/// from one: a variable the build's walk knows as a type a conversation isn't made from (a list, a text) is left out.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    public @this() : base("") { }

    protected internal override string Owner => "conversation";

    /// <summary>The step's variables, but those the walk knows (<c>step.Typed</c>) as a type a conversation doesn't take.
    /// One the walk can't type stays offered: that stands in for an llm's answer — llm.query forwards a Data of no type
    /// the build knows — and so does any untyped forwarder's result (goal.call's); it is a candidate, never a
    /// guarantee. None left, the decider is offered only none.</summary>
    public override async System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>> Offers(global::app.goal.step.@this step)
        => (await base.Offers(step))
            .Where(offer => offer is not global::app.type.item.variable.@this variable
                || step.Typed[variable.Code.Root.Name]?.Type is not { } known
                || global::app.module.llm.type.conversation.@this.Takes(known))
            .ToList();
}
