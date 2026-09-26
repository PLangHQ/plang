using app.Attributes;
using app.variable;
using app.module.action.llm.code;

namespace app.module.action.llm;

/// <summary>
/// Asks a decision service a set of typed questions about one shared state, and answers each.
///
/// <para>This is not a chat: the state is handed over ONCE and every question is asked against it,
/// so N questions cost one round trip and each answer is independent of the others' wording. Two
/// question types: a <c>noul</c> answers a yes/no with a probability, a <c>choice</c> picks one of
/// a closed set. That is what makes it usable for building — the builder asks "does this step use
/// module X" of every module at once, then "which action of X" of the ones that said yes.</para>
/// </summary>
[Action("decider")]
[RequiresCapability("llm")]
public partial class decider : IContext
{
    /// <summary>The shared context every question is asked against — for the builder, a text: how
    /// plang is structured, the goal and its steps, and the modules (decider.state.template). Handed
    /// over once.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> State { get; init; }

    /// <summary>The questions, keyed by an id the caller chooses so it can match answers back.
    /// Each is <c>{type, instructions}</c> — a <c>choice</c> adds <c>criteria</c>, the closed set it
    /// must pick from.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.dict.@this> Question { get; init; }

    [Default("jev-latest")]
    public partial data.@this<global::app.type.item.text.@this> Model { get; init; }

    [Code]
    public partial IDecider Decider { get; }

    /// <summary>One answer per question, under the id it was asked with: a noul answers a
    /// probability, a choice answers its pick and a confidence.</summary>
    public async Task<data.@this<global::app.type.item.dict.@this>> Run() => await Decider.Decide(this);
}
