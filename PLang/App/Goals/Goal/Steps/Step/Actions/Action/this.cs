using System.Text.Json.Serialization;
using App.Variables;
namespace App.Goals.Goal.Steps.Step.Actions.Action;

/// <summary>
/// A single action within a step — the LLM-mapped unit of execution.
/// Identifies the module and handler to invoke, with typed parameters, return mappings, and defaults.
/// </summary>
public sealed partial class @this : modules.IDataWrappable
{
    /// <summary>
    /// OBP: Action is responsible for its own Data representation.
    /// Returns a cached per-execution Data&lt;Action&gt; wrapper from the context.
    /// </summary>
    public Data.@this AsData(Actor.Context.@this context)
    {
        return context.GetOrCreate(this, () =>
        {
            var data = new Data.@this<@this>("", this);
            data.Context = context;
            return data;
        });
    }

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
    /// Runs this action: lifecycle events → dispatch → return mapping.
    /// Context travels as parameter — actions are shared objects, not per-request.
    /// </summary>
    public async Task<Data.@this> RunAsync(Actor.Context.@this context)
    {
        var lifecycle = context.LifecycleFor(this);

        // BeforeAction events — can override execution via Handled
        var beforeResult = await lifecycle.Before.Run(context, App.Events.EventType.BeforeAction);
        if (!beforeResult.Success) return beforeResult;
        if (beforeResult.Handled) return beforeResult;

        // Dispatch to handler
        var result = await context.App!.Run(this, context);

        // Store result as %__data__% — available to next action or step
        if (result.Success)
        {
            result.Name = "__data__";
            context.Variables.Put(result);
        }

        // AfterAction events
        var afterResult = await lifecycle.After.Run(context, App.Events.EventType.AfterAction);
        if (!afterResult.Success) return afterResult;

        return result;
    }

    /// <summary>
    /// Return type properties for the builder summary. Null when Run() returns plain Data.
    /// Derived from the concrete return type of Run() via reflection in Describe().
    /// </summary>
    public List<Data.@this>? ReturnType { get; init; }
}
