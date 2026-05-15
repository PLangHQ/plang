using app.variables;
using app.Code;

namespace app.modules.llm.code;

/// <summary>
/// LLM provider interface. The provider owns the full lifecycle:
/// format messages, call API, handle tool loop, return result.
/// Swappable via app.Code.
/// </summary>
public interface ILlm : ICode
{
    Task<data.@this> Query(query action);
}
