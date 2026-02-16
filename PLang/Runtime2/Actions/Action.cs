using System.Text.Json.Serialization;
using PLang.Runtime2.Memory;
using PLang.Runtime2.Serialization;

namespace PLang.Runtime2;

public sealed partial class Action
{
    [JsonIgnore]
    public System.Type? ParameterSchema { get; init; }
    [Store, LlmBuilder, Debug, Default]
    [JsonPropertyName("module")]
    [Newtonsoft.Json.JsonProperty("module")]
    public string Module { get; init; } = "";

    [Store, LlmBuilder, Debug, Default]
    [JsonPropertyName("action")]
    [Newtonsoft.Json.JsonProperty("action")]
    public string ActionName { get; init; } = "";

    [Store, LlmBuilder, Debug, Default]
    public List<Data> Parameters { get; init; } = new();

    [Store, LlmBuilder, Debug, Default]
    public List<Data>? Return { get; init; }

    [Store, LlmBuilder, Debug, Default]
    public List<Info> Errors { get; init; } = new();

    [Store, LlmBuilder, Debug, Default]
    public List<Info> Warnings { get; init; } = new();

    [JsonIgnore]
    public bool Cacheable { get; init; } = true;
}
