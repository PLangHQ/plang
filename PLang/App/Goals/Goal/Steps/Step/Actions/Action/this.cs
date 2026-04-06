using System.Text.Json.Serialization;
using App.Variables;
namespace App.Goals.Goal.Steps.Step.Actions.Action;

public sealed partial class @this : Data.@this<@this>
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
    public List<Data.@this> Parameters { get; init; } = new();

    [Store, LlmBuilder, Debug, Default]
    public List<Data.@this>? Return { get; init; }

    [Store, Debug, Default]
    public List<Data.@this>? Defaults { get; set; }

    [Debug]
    public List<Info> Errors { get; init; } = new();

    [Debug]
    public List<Info> Warnings { get; init; } = new();

    [JsonIgnore]
    public bool Cacheable { get; init; } = true;

    [JsonIgnore]
    public Steps.Step.@this? Step { get; set; }

    private modules.Events? _events;
    [JsonIgnore]
    public modules.Events Events
    {
        get => _events ??= new modules.Events(this);
    }

    public List<Data.@this> Examples { get; init; } = new();

    /// <summary>
    /// Runs this action, switching to the target actor context if specified.
    /// </summary>
    public async Task<Data.@this> RunAsync(App.@this app, Context.@this context, Context.Actor? targetActor = null)
    {
        if (targetActor != null && targetActor != context.Actor)
        {
            var previousActor = app.CurrentActor;
            app.CurrentActor = targetActor;
            try
            {
                return await app.Run(this, targetActor.Context);
            }
            finally
            {
                app.CurrentActor = previousActor;
            }
        }

        return await app.Run(this, context);
    }

    /// <summary>
    /// Return type properties for the builder summary. Null when Run() returns plain Data.
    /// Derived from the concrete return type of Run() via reflection in Describe().
    /// </summary>
    public List<Data.@this>? ReturnType { get; init; }
}
