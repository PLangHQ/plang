using System.Text.Json.Serialization;

namespace PLang.Building.Model
{
	public record LlmQuestion(string type, string? system, string question, string? assistant, string model = "gpt-4", bool caching = true)
	{
		internal double? temperature;
		internal double? top_p;
		internal double? frequencyPenalty;
		internal double? presencePenalty;
		internal int maxLength = 4000;

		[JsonIgnore]
		public bool Reload { get; internal set; }
		[JsonIgnore]
		public string? PreviousResult { get; internal set; }
		public string? RawResponse { get; set; }
	}
}
