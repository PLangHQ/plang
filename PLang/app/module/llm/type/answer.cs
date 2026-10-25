namespace app.module.llm.type;

/// <summary>Where the llm's answers come from: the answers saved beside a test (a question not there asked once and
/// added), or live, every question asked of the llm and nothing saved.</summary>
[global::app.Attributes.PlangType("answer"), global::app.Attributes.Choice]
public enum answer
{
    saved,
    live,
}
