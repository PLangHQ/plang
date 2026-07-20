using System.Text.Json.Serialization;
using app.actor.context;
using app.data;
using app.variable;
using app.module;
using ActionEl = app.goal.step.action.@this;

namespace app.goal.step;

/// <summary>
/// Represents a step within a goal for App.
/// </summary>
public sealed partial class @this
{
    // A step is a plain C# host — carried as clr<step>, reflected off its [Store] props.


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
    /// Goal.Merge when actions are carried over from an existing .pr. Used
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

    private global::app.goal.step.action.list.@this _action = new(new List<ActionEl>());
    [Store, Debug, Default]
    public global::app.goal.step.action.list.@this Action
    {
        // The step owns its action chain (an action.list node). The getter stamps the back-ref so a
        // navigated / executed action reaches its step.
        get { foreach (var a in _action.list) a.Step ??= this; return _action; }
        set => _action = value ?? new(new List<ActionEl>());
    }

    /// <summary>
    /// Nests each modifier onto the preceding action's Modifiers slot — the flat LLM order becomes
    /// the .pr shape. A modifier is a TYPE in the catalog (not a flag): the flat item, read as a plain
    /// action, becomes the modifier it IS, with Position from the catalog. A leading modifier with no
    /// preceding action is dropped with a warning. Rebuilds the action node. (Carried only until the
    /// builder emits nested — then it is a no-op.)
    /// </summary>
    public void Nest(global::app.module.list.@this modules)
    {
        var flat = _action.list.ToList();
        if (flat.Count == 0) return;

        var nested = new List<ActionEl>();
        ActionEl? current = null;

        foreach (var a in flat)
        {
            if (modules.Contains(a.Module)
                && modules[a.Module][a.ActionName] is action.modifier.@this catalog)
            {
                if (current == null)
                {
                    Warnings.Add(new Info
                    {
                        Key = "DroppedLeadingModifier",
                        Message = $"Modifier '{a.Module}.{a.ActionName}' has no preceding action and was dropped"
                    });
                    continue;
                }
                current.Modifiers.Add(new action.modifier.@this
                    { Module = a.Module, ActionName = a.ActionName, Parameters = a.Parameters, Position = catalog.Position });
            }
            else
            {
                current = a;
                nested.Add(a);
            }
        }

        foreach (var a in nested)
            a.Modifiers.Sort((x, y) => x.Position.CompareTo(y.Position));   // outermost wrapper (lowest Position) first

        _action = new global::app.goal.step.action.list.@this(nested);
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
    /// Lazy — navigates to parent Goal.Step on access.
    /// Used by the builder to omit GoalIfTrue/GoalIfFalse when sub-steps handle branching.
    /// </summary>
    [JsonIgnore]
    public bool HasSubSteps => System.Linq.Enumerable.Any(_action.list, a => a.Child.Count > 0);

    [JsonIgnore]
    public global::app.goal.@this Goal { get; set; } = null!;

    /// <summary>
    /// Runs this step: lifecycle events → actions.
    /// Error handling, caching, and timeouts are per-action modifiers, not step-level.
    /// </summary>
    public async Task<data.@this> Run(actor.context.@this context)
    {
        context.Step = this;
        var lifecycle = context.LifecycleFor(this);

        var beforeResult = await lifecycle.Before.Run(context, app.@event.Trigger.BeforeStep);
        if (!beforeResult.Success) return beforeResult;
        if (beforeResult.Handled) return beforeResult;

        data.@this result;
        try
        {
            result = await Action.Run(context);   // action.list owns the chain loop + fire
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
    /// Merges LLM-derived fields from another step onto this step.
    /// Structural fields (Text, Index, Indent, LineNumber) are untouched.
    /// </summary>
    public void Merge(Step from)
    {
        if (from.Action.Count > 0)
            _action = new global::app.goal.step.action.list.@this(from.Action.list);

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
