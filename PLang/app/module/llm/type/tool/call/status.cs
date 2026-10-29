namespace app.module.llm.type.tool.call;

/// <summary>Where a tool call is, as its OnToolCall callback is told: about to run, or run.</summary>
[global::app.Attributes.PlangType("tool.call.status"), global::app.Attributes.Choice]
public enum status
{
    starting,
    completed,
}
