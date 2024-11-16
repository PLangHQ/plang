using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace PLang.Models;

public record LlmRequest(string type, List<LlmMessage> promptMessage, string model = "gpt-4o", bool caching = true)
{
    public double frequencyPenalty = 0;
    public string llmResponseType = "json";
    public int maxLength = 4000;
    public string model = model;
    public double presencePenalty = 0;
    public List<LlmMessage> promptMessage = promptMessage;
    public string? scheme = null;
    public double temperature = 0;
    public double top_p = 0;

    [JsonIgnore]
    [IgnoreDataMember]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Reload { get; internal set; }

    [JsonIgnore]
    [IgnoreDataMember]
    [System.Text.Json.Serialization.JsonIgnore]
    public string? PreviousResult { get; internal set; }

    public string? RawResponse { get; set; }
}