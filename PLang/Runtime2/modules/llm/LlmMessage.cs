using PLang.Runtime2.Engine;

namespace PLang.Runtime2.modules.llm;

/// <summary>
/// Message in an LLM conversation. Role + Text + optional Images for multimodal.
/// ToolCallId and ToolCalls are internal — used by the provider during tool conversations,
/// never set by the builder.
/// </summary>
public class LlmMessage
{
    [Store, LlmBuilder]
    public string Role { get; set; } = "";

    [Store, LlmBuilder]
    public string? Text { get; set; }

    [Store, LlmBuilder]
    public List<string>? Images { get; set; }

    // --- Tool conversation fields (internal, not exposed to builder) ---

    /// <summary>For role=tool: which call this responds to.</summary>
    public string? ToolCallId { get; set; }

    /// <summary>For role=assistant: tools the LLM wants to call.</summary>
    public List<ToolCall>? ToolCalls { get; set; }
}
