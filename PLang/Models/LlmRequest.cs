using System.Runtime.Serialization;

namespace PLang.Models
{
    public record LlmRequest(string type, List<LlmMessage> promptMessage, string model = "gpt-4-turbo", bool caching = true)
    {
        public double temperature = 0;
        public double top_p = 0;
        public double frequencyPenalty = 0;
        public double presencePenalty = 0;
        public int maxLength = 4000;
        public string llmResponseType = "json";
        public string? scheme = null;

        [Newtonsoft.Json.JsonIgnore]
        [IgnoreDataMember]

        [System.Text.Json.Serialization.JsonIgnore]
        public bool Reload { get; internal set; }
        [Newtonsoft.Json.JsonIgnore]
        [IgnoreDataMember]

        [System.Text.Json.Serialization.JsonIgnore]
        public string? PreviousResult { get; internal set; }
        public string? RawResponse { get; set; }
    }
}
