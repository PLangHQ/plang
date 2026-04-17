using System.Text.Json.Serialization;
using App.Actor.Context;
using App.Variables;
using App.modules;
using Action = App.Goals.Goal.Steps.Step.Actions.Action.@this;

namespace App.Goals.Goal.Steps.Step;

/// <summary>
/// Represents a step within a goal for App.
/// </summary>
public sealed partial class @this : modules.IDataWrappable
{
    [JsonIgnore]
    public Actor.Context.@this? Context { get; set; }

    /// <summary>
    /// Whether this step is disabled for the current execution.
    /// Backed by context storage so concurrent executions don't interfere.
    /// Set by the condition module when a condition is false — marks indented sub-steps.
    /// </summary>
    [JsonIgnore]
    public bool Disabled
    {
        get
        {
            if (Context == null) return false;
            return Context.Get<bool>(DisabledKey);
        }
        set
        {
            if (value)
                Context?.Set(DisabledKey, true);
            else
                Context?.Set<bool>(DisabledKey, default); // removes from context
        }
    }

    private string DisabledKey => $"step:{Goal?.PrPath}:{Index}:disabled";

    private modules.Events? _events;
    [JsonIgnore]
    public modules.Events Events
    {
        get => _events ??= new modules.Events(this);
        set => _events = value;
    }
    [Store, LlmBuilder, Debug, Default]
    public int Index { get; init; }

    [Store, LlmBuilder, Debug, Default]
    public string Text { get; init; } = "";

    [Store, Debug, Default]
    public int LineNumber { get; init; }

    [Store, LlmBuilder, Debug, Default]
    public int Indent { get; init; }

    [Store, LlmBuilder, Debug, Default]
    public string? Comment { get; init; }

    private Actions.@this _actions = new();
    [Store, Debug, Default]
    public Actions.@this Actions
    {
        get { _actions.Step = this; return _actions; }
        set => _actions = value ?? new();
    }

    /// <summary>
    /// Computed hash of the step text. Used by Setup for idempotency tracking.
    /// </summary>
    [JsonIgnore]
    public string? Hash => string.IsNullOrEmpty(Text) ? null
        : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Text))).ToLowerInvariant();

    [Store, LlmBuilder, Debug, Default]
    public string? Intent { get; init; }

    [Debug]
    public List<Info> Errors { get; init; } = new();

    [Debug]
    public List<Info> Warnings { get; init; } = new();

    [Store, Debug, Default]
    public bool WaitForExecution { get; init; } = true;

    /// <summary>
    /// True when the next step has higher indent (sub-steps).
    /// Lazy — navigates to parent Goal.Steps on access.
    /// Used by the builder to omit GoalIfTrue/GoalIfFalse when sub-steps handle branching.
    /// </summary>
    [JsonIgnore]
    public bool HasSubSteps => Goal?.Steps.HasIndentedChildren(Index) ?? false;

    [JsonIgnore]
    public Goals.Goal.@this? Goal { get; set; }

    /// <summary>
    /// Runs this step: lifecycle events → actions.
    /// Error handling, caching, and timeouts are per-action modifiers, not step-level.
    /// </summary>
    public async Task<Data.@this> RunAsync(Actor.Context.@this context)
    {
        context.Step = this;
        var lifecycle = context.LifecycleFor(this);

        var beforeResult = await lifecycle.Before.Run(context, App.Events.EventType.BeforeStep);
        if (!beforeResult.Success) return beforeResult;
        if (beforeResult.Handled) return beforeResult;

        Data.@this result = Data.@this.Ok();
        try
        {
            foreach (var action in Actions)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                result = await action.RunAsync(context);
                if (!result.Success || result.Handled) break;
            }
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or OperationCanceledException))
        {
            result = Data.@this.FromError(new Errors.ServiceError(
                ex.Message, "StepError", 400) { Exception = ex });
        }

        var afterResult = await lifecycle.After.Run(context, App.Events.EventType.AfterStep);
        if (!afterResult.Success) return afterResult;

        return result;
    }

    /// <summary>
    /// OBP: Step is responsible for its own Data representation.
    /// Returns a cached per-execution Data&lt;Step&gt; wrapper from the context.
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

    public @this Clone()
    {
        return new @this
        {
            Index = Index,
            Text = Text,
            LineNumber = LineNumber,
            Indent = Indent,
            Comment = Comment,
            Actions = new Actions.@this(Actions.Select(a => new Action
            {
                Module = a.Module,
                ActionName = a.ActionName,
                Parameters = new List<Data.@this>(a.Parameters),
                Defaults = a.Defaults != null ? new List<Data.@this>(a.Defaults) : null,
                Errors = new List<Info>(a.Errors),
                Warnings = new List<Info>(a.Warnings),
                Modifiers = new ActionModifiers(a.Modifiers.Select(m => new Action
                {
                    Module = m.Module,
                    ActionName = m.ActionName,
                    Parameters = new List<Data.@this>(m.Parameters),
                    Defaults = m.Defaults != null ? new List<Data.@this>(m.Defaults) : null,
                    Errors = new List<Info>(m.Errors),
                    Warnings = new List<Info>(m.Warnings)
                }))
            })),
            WaitForExecution = WaitForExecution,
            Goal = Goal,
            Intent = Intent,
            Errors = new List<Info>(Errors),
            Warnings = new List<Info>(Warnings)
        };
    }

    /// <summary>
    /// Merges LLM-derived fields from another step onto this step.
    /// Structural fields (Text, Index, Indent, LineNumber) are untouched.
    /// </summary>
    public void Merge(Step.@this from)
    {
        if (from.Actions.Count > 0)
        {
            Actions.Clear();
            Actions.AddRange(from.Actions);
        }

        if (from.Errors.Count > 0)
        {
            Errors.Clear();
            Errors.AddRange(from.Errors);
        }

        if (from.Warnings.Count > 0)
        {
            Warnings.Clear();
            Warnings.AddRange(from.Warnings);
        }
    }

    public override string ToString() => $"[{Index}] {Text}";
}
