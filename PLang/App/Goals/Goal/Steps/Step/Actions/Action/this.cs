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

    [Store, Debug, Default]
    public Modifiers.@this Modifiers { get; init; } = new();

    [Debug]
    public List<Info> Errors { get; init; } = new();

    [Debug]
    public List<Info> Warnings { get; init; } = new();

    [JsonIgnore]
    public bool Cacheable { get; init; } = true;

    /// <summary>
    /// True for any condition chain action: condition.if, condition.elseif, or condition.else.
    /// Used by SplitAtConditions / ComputeBranchChain to split an orchestrated step's actions
    /// into per-branch groups.
    /// </summary>
    [JsonIgnore]
    public bool IsCondition =>
        string.Equals(Module, "condition", StringComparison.OrdinalIgnoreCase) &&
        (string.Equals(ActionName, "if", StringComparison.OrdinalIgnoreCase)
      || string.Equals(ActionName, "elseif", StringComparison.OrdinalIgnoreCase)
      || string.Equals(ActionName, "else", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True only for the head of a condition chain (condition.if). Coverage records sites
    /// against the head — elseif/else participate in the chain but don't own the site.
    /// </summary>
    [JsonIgnore]
    public bool IsIfHead =>
        string.Equals(Module, "condition", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(ActionName, "if", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when this is the first condition.if action in its step. Used by coverage
    /// to ignore inner-elseif simple-path firings that would otherwise mix
    /// true/false labels into the orchestrator's declared chain.
    /// </summary>
    [JsonIgnore]
    public bool IsFirstConditionInStep => Step?.Actions.IsFirstCondition(this) ?? false;

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
    /// One-sentence description of what this action does, sourced from
    /// [System.ComponentModel.Description] on the action class.
    /// Populated by Modules.Describe(); null when no description attribute is present.
    /// </summary>
    [JsonIgnore]
    public string? Description { get; init; }

    /// <summary>
    /// One-sentence description of the module this action belongs to, sourced from
    /// [ModuleDescription] on the first action class in the module namespace.
    /// Populated by Modules.Describe(); null when no attribute is present.
    /// </summary>
    [JsonIgnore]
    public string? ModuleDescription { get; init; }

    /// <summary>
    /// True when this action is declared as a modifier via [Modifier] on its class.
    /// Modifier actions wrap the preceding action rather than standing on their own
    /// (e.g. cache.wrap, error.handle, timeout.after). The catalog renders modifier
    /// actions in their own section. Per-action, so a module can carry a mix —
    /// e.g. error.throw (action) and error.handle (modifier) share the error module.
    /// </summary>
    [JsonIgnore]
    public bool IsModifier { get; init; }

    /// <summary>
    /// Runs this action: lifecycle events → dispatch → return mapping.
    /// Context travels as parameter — actions are shared objects, not per-request.
    /// </summary>
    public async Task<Data.@this> RunAsync(Actor.Context.@this context)
    {
        var lifecycle = context.LifecycleFor(this);

        var beforeResult = await lifecycle.Before.Run(context, App.Events.EventType.BeforeAction);
        if (!beforeResult.Success) return beforeResult;

        Data.@this result;
        if (beforeResult.Handled)
        {
            // Override path: the BeforeAction binding supplied this action's result
            // (mock.intercept, event.skipAction). Clear Handled so the outer step
            // loop doesn't misread "dispatch was short-circuited" as "stop the step" —
            // the next action in the chain still needs to run on this result.
            result = beforeResult;
            result.Handled = false;
        }
        else
        {
            Func<Task<Data.@this>> dispatch = () => context.App!.Run(this, context);
            result = await Modifiers.RunAsync(dispatch, context);
        }

        if (result.Success)
        {
            // Alias the result as %__data__% — Variables.Set stores Data references as-is
            // (no clone, no rename). Same object is reachable via both %__data__% and
            // whatever name the producing handler owns (e.g., variable.set's stored entry).
            // Override path (beforeResult.Handled) flows through the same write so
            // mocks and event.skipAction feed the next action like any real result.
            context.Variables.Set("__data__", result);
        }

        var afterResult = await lifecycle.After.Run(context, App.Events.EventType.AfterAction, this, result);
        if (!afterResult.Success) return afterResult;

        return result;
    }

    /// <summary>
    /// Wraps the given inner delegate with this modifier action. Resolves this action's
    /// handler, verifies it implements IModifier, and runs ExecuteAsync so the source-generated
    /// properties are populated before Wrap() reads them. Called by Modifiers.RunAsync.
    /// </summary>
    public async Task<(Func<Task<Data.@this>>? Wrapped, Errors.IError? Error)> WrapAround(
        Func<Task<Data.@this>> next,
        Actor.Context.@this context)
    {
        var (handler, error) = context.App!.Modules.GetCodeGenerated(this);
        if (error != null) return (null, error);
        if (handler is not modules.IModifier mod)
            return (null, new Errors.ActionError(
                $"{Module}.{ActionName} is not a modifier", "ModifierError", 400));

        await handler.ExecuteAsync(this, context);
        return (mod.Wrap(next, context), null);
    }

    /// <summary>
    /// Return type properties for the builder summary. Null when Run() returns plain Data.
    /// Derived from the concrete return type of Run() via reflection in Describe().
    /// </summary>
    public List<Data.@this>? ReturnType { get; init; }
}
