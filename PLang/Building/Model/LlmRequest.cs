using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace PLang.Building.Model
{
	public record LlmRequest(string type, string? promptMessage, string model = "gpt-4", bool caching = true)
	{
		public double? temperature;
		public double? top_p;
		public double? frequencyPenalty;
		public double? presencePenalty;
		public int maxLength = 4000;

		[Newtonsoft.Json.JsonIgnore]
[IgnoreDataMemberAttribute]

[System.Text.Json.Serialization.JsonIgnore]
		public bool Reload { get; internal set; }
		[Newtonsoft.Json.JsonIgnore]
[IgnoreDataMemberAttribute]

[System.Text.Json.Serialization.JsonIgnore]
		public string? PreviousResult { get; internal set; }
		public string? RawResponse { get; set; }
	}
}
