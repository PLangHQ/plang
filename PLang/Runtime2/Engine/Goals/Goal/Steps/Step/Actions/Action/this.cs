using System.Text.Json.Serialization;
using PLang.Runtime2.Engine.Memory;
namespace PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action;

public sealed partial class @this
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

    [Store, Debug, Default]
    public List<Data>? Defaults { get; set; }

    [Debug]
    public List<Info> Errors { get; init; } = new();

    [Debug]
    public List<Info> Warnings { get; init; } = new();

    [JsonIgnore]
    public bool Cacheable { get; init; } = true;

    public List<Data> Examples { get; init; } = new();

    /// <summary>
    /// Return type properties for the builder summary. Null when Run() returns plain Data.
    /// Derived from the concrete return type of Run() via reflection in Describe().
    /// </summary>
    public List<Data>? ReturnType { get; init; }
}
