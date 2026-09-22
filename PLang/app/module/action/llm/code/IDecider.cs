using app.variable;
using app.module.action.code;

namespace app.module.action.llm.code;

/// <summary>
/// Decision-service provider. Takes one shared state and a set of typed questions, and answers
/// every question against that state in a single exchange. Swappable via app.Code.
/// </summary>
public interface IDecider : ICode
{
    /// <summary>One answer per question, keyed by the id it was asked under.</summary>
    Task<data.@this<global::app.type.item.dict.@this>> Decide(decider action);
}
