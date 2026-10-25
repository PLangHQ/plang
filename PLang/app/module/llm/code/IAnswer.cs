using app.module.code;

namespace app.module.llm.code;

/// <summary>
/// Where an llm request is answered: OpenAI itself (<see cref="Live"/>, shipped), or a test's saved answers
/// (<see cref="Saved"/>, which a test registers on its own app). Chosen once for the app through app.Code; the
/// query sends every request through it.
/// </summary>
public interface IAnswer : ICode
{
    /// <summary>The answer to <paramref name="body"/>, the request as the API takes it, for <paramref name="action"/>:
    /// the response's body, or the error that came back.</summary>
    Task<data.@this> Ask(data.@this body, query action);
}
