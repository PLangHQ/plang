using System.Text.Json.Serialization;
using app.actor.context;
using app.data;
using app.variable;
using app.module;
using Action = app.goal.steps.step.actions.action.@this;

namespace app.goal.steps.step;

/// <summary>
/// Represents a step within a goal for App.
/// </summary>
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, module.IDataWrappable
{
    /// <summary>Self-write: a step is a structural item — its tagged fields (View selects the set).</summary>
    public override System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
        => OutputTagged(writer, mode, context);

    /// <summary>
    /// Whether this step is disabled for the given execution.
    /// Per-execution state keyed by step identity, reached by passing the running
    /// context — concurrent executions don't interfere. Set by the condition module
    /// when a condition is false to mark indented sub-steps.
    /// </summary>
    public bool Disabled(actor.context.@this context) => context.Get<bool>(DisabledKey);

    /// <summary>Disables this step for the given execution.</summary>
    public void Disable(actor.context.@this context) => context.Set(DisabledKey, true);

    /// <summary>Re-enables this step for the given execution (clears the disabled flag).</summary>
    public void Enable(actor.context.@this context) => context.Set<bool>(DisabledKey, default);

    private string DisabledKey => $"step:{Goal?.PrPath}:{Index}:disabled";

    private module.Events? _events;
    [JsonIgnore]
    public module.Events Events
    {
        get => _events ??= new module.Events(this);
        set => _events = value;
    }
    [Store, LlmBuilder, Debug, Default]
    public int Index { get; init; }

    [Store, LlmBuilder, Debug, Default]
    public string Text { get; init; } = "";

    /// <summary>
    /// Text of the prior-build step that produced this step's Actions. Set by
    /// Goal.MergeFrom when actions are carried over from an existing .pr. Used
    /// by the builder prompt to decide between @known (PriorText == Text) and
    /// @hint (PriorText != Text). Transient — not serialized to .pr.
    /// </summary>
    [JsonIgnore]
    public string? PriorText { get; set; }

    [Store, Debug, Default]
    public int LineNumber { get; init; }

    [Store, LlmBuilder, Debug, Default]
    public int Indent { get; init; }

    [Store, LlmBuilder, Debug, Default]
    public string? Comment { get; init; }

    private actions.@this _actions = new();
    [Store, Debug, Default]
    public actions.@this Actions
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

    /// <summary>LLM's formalized rendering of this step (action.module Param=value | …). Stored for traces.</summary>
    [Store, Debug, Default]
    public string? Formal { get; set; }

    /// <summary>Tag set by enrichResponse: "known" (prior text matched), "hint" (text changed, prior available), or "new".</summary>
    [Store, Debug, Default]
    public string? Source { get; set; }

    /// <summary>Build-time only: LLM signal to reuse prior actions. Not stored in .pr (no [Store]).</summary>
    public bool Keep { get; set; }

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
    public global::app.goal.@this Goal { get; set; } = null!;

    /// <summary>
    /// Runs this step: lifecycle events → actions.
    /// Error handling, caching, and timeouts are per-action modifiers, not step-level.
    /// </summary>
    public async Task<data.@this> RunAsync(actor.context.@this context)
    {
        context.Step = this;
        var lifecycle = context.LifecycleFor(this);

        var beforeResult = await lifecycle.Before.Run(context, app.@event.Trigger.BeforeStep);
        if (!beforeResult.Success) return beforeResult;
        if (beforeResult.Handled) return beforeResult;

        data.@this result = context.Ok();
        try
        {
            foreach (var action in Actions)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                result = await action.RunAsync(context);
                if (result.ShouldExit() || result.Handled) break;
            }
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or OperationCanceledException))
        {
            // Preserve the exception's class identity as the error Key so on-error
            // handlers keyed on a typed exception (e.g. ChannelNotFoundException →
            // "ChannelNotFound") still match. Falls back to "StepError" only when
            // the exception is the bare base type. Trims trailing "Exception".
            var typeName = ex.GetType().Name;
            var key = typeName == nameof(Exception)
                ? "StepError"
                : (typeName.EndsWith("Exception", StringComparison.Ordinal)
                    ? typeName[..^"Exception".Length]
                    : typeName);
            result = context.Error(new global::app.error.ServiceError(
                ex.Message, key, 400) { Exception = ex });
        }

        var afterResult = await lifecycle.After.Run(context, app.@event.Trigger.AfterStep);
        if (!afterResult.Success) return afterResult;

        return result;
    }

    /// <summary>
    /// OBP: Step is responsible for its own Data representation.
    /// Returns a cached per-execution Data&lt;Step&gt; wrapper from the context.
    /// </summary>
    public data.@this AsData(actor.context.@this context)
    {
        return context.GetOrCreate(this, () =>
        {
            var data = new data.@this<@this>("", this);
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
            Actions = new actions.@this(Actions.Select(a => new Action
            {
                Module = a.Module,
                ActionName = a.ActionName,
                Parameters = new List<data.@this>(a.Parameters),
                Defaults = a.Defaults != null ? new List<data.@this>(a.Defaults) : null,
                Errors = new List<Info>(a.Errors),
                Warnings = new List<Info>(a.Warnings),
                Modifiers = new ActionModifiers(a.Modifiers.Select(m => new Action
                {
                    Module = m.Module,
                    ActionName = m.ActionName,
                    Parameters = new List<data.@this>(m.Parameters),
                    Defaults = m.Defaults != null ? new List<data.@this>(m.Defaults) : null,
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
    public void Merge(Step from)
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
