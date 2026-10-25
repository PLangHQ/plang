namespace app.test;

/// <summary>Where a test's llm questions are answered: from the answers saved beside it (a question not there is asked
/// once and added), or live, every one asked of the llm and nothing saved.</summary>
[global::app.Attributes.PlangType("llm"), global::app.Attributes.Choice]
public enum llm
{
    saved,
    live,
}
