using app.module.code;

namespace app.module.llm.code;

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
    Task<data.@this> Query(query action);

    /// <summary>Fired before each request goes out, with its resolved messages and its schema (if any); each subscriber
    /// is awaited first. Debug's LLM tracing subscribes to it, whichever provider answers.</summary>
    event Func<List<LlmMessage>, string?, Task>? OnBeforeRequest;

    /// <summary>Fired with the raw response after each successful request; each subscriber is awaited before the result
    /// is built.</summary>
    event Func<string, Task>? OnAfterResponse;
}
