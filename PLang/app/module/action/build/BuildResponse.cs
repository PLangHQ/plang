namespace app.module.action.build;

/// <summary>
/// Typed model of the LLM build response. Mirrors the JSON schema sent to the LLM
/// by the compiler prompt (Compile.llm). validateResponse and enrichResponse work
/// against this — no JsonElement / IDictionary forks. The framework deserializes
/// the LLM's JsonElement to this via data.@this&lt;BuildResponse&gt; using
/// Json.CaseInsensitiveRead.
/// </summary>
public sealed partial class BuildResponse : global::app.type.item.@this, global::app.type.item.ICreate<BuildResponse>
{
    public string? Description { get; set; }
    public List<Info>? Errors { get; set; }
    public List<Info>? Warnings { get; set; }
    public List<Step> Steps { get; set; } = new();
}
