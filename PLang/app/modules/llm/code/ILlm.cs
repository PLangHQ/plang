using app.variable;
using app.modules.code;

namespace app.modules.llm.code;

/// <summary>
/// LLM provider interface. The provider owns the full lifecycle:
/// format messages, call API, handle tool loop, return result.
/// Swappable via app.Code.
/// </summary>
public interface ILlm : ICode
{
    // Polymorphic — response shape depends on Schema/tool config (raw string,
    // structured object, tool-call object). The action declares Data<object>;
    // every implementation must match.
    Task<data.@this<object>> Query(query action);
}
