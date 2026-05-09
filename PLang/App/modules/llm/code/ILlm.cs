using App.Variables;
using App.Code;

namespace App.modules.llm.code;

/// <summary>
/// LLM provider interface. The provider owns the full lifecycle:
/// format messages, call API, handle tool loop, return result.
/// Swappable via app.Code.
/// </summary>
public interface ILlm : ICode
{
    Task<Data.@this> Query(query action);
}
