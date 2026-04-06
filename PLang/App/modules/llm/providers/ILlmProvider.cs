using App.Variables;
using App.Providers;

namespace App.modules.llm.providers;

/// <summary>
/// LLM provider interface. The provider owns the full lifecycle:
/// format messages, call API, handle tool loop, return result.
/// Swappable via engine.Providers.
/// </summary>
public interface ILlmProvider : IProvider
{
    Task<Data> Query(query action);
}
