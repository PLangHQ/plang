using System.Text.Json.Serialization;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;
using Action = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this;

namespace PLang.Runtime2.Engine.Goals.Goal.Steps.Step;

/// <summary>
/// Represents a step within a goal for Runtime2.
/// </summary>
public sealed partial class @this
{
    [JsonIgnore]
    public PLangContext? Context { get; set; }

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
        set => _actions = value;
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

    [Store, Debug, Default]
    public ErrorHandler? OnError { get; set; }

    [Store, Debug, Default]
    public CacheSettings? Cache { get; set; }

    [Store, Debug, Default]
    public int? Timeout { get; init; }

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
                Parameters = new List<Data>(a.Parameters),
                Return = a.Return != null ? new List<Data>(a.Return) : null,
                Defaults = a.Defaults != null ? new List<Data>(a.Defaults) : null,
                Errors = new List<Info>(a.Errors),
                Warnings = new List<Info>(a.Warnings)
            })),
            WaitForExecution = WaitForExecution,
            Goal = Goal,
            Intent = Intent,
            OnError = OnError,
            Cache = Cache,
            Timeout = Timeout,
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

        if (from.Cache != null)
            Cache = from.Cache;

        if (from.OnError != null)
            OnError = from.OnError;

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
