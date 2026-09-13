using PLang.Building.Model;

namespace PLang.Models
{
	// A tool the llm may call during an agent run: a JSON schema for its arguments and the goal that
	// runs it. The arguments become the goal's parameters, the goal's return value is the tool result.
	public record AgentTool(string Name, string Description, object? Parameters, string Call);

	public record LlmToolCall(string Id, string Name, string Arguments);

	public record LlmUsage(long InputTokens, long OutputTokens);

	// One round trip: everything the service sent back, as items in the provider's own shape (so a
	// reasoning item can be handed back on the next request), plus the parsed parts the loop needs.
	public record LlmChatResult(List<object> Items, string? Text, List<LlmToolCall> ToolCalls, LlmUsage Usage);

	public class LlmChatRequest
	{
		public string? Model { get; set; }
		public string? Reasoning { get; set; }
		public IList<object> Messages { get; set; } = new List<object>();
		public List<AgentTool>? Tools { get; set; }
		public object? TextFormat { get; set; }
		public int TimeoutInSeconds { get; set; } = 600;
		public GoalStep? Step { get; set; }
	}
}
