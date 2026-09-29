using System.Text.Json.Serialization;
using app.actor.context;
using app.data;
using app.module;

namespace app.goal.step;

/// <summary>
/// Represents a step within a goal for App.
/// </summary>
public sealed partial class @this
{
    // A step is a plain C# host — carried as clr<step>, reflected off its [Store] props.


    [Store, LlmBuilder, Debug, Default]
    public int Index { get; internal set; }

    [Store, LlmBuilder, Debug, Default]
    public string Text { get; internal set; } = "";

    /// <summary>
    /// Text of the prior-build step that produced this step's Actions. Set by
    /// Goal.Merge when actions are carried over from an existing .pr. Used
    /// by the builder prompt to decide between @known (PriorText == Text) and
    /// @hint (PriorText != Text). Transient — not serialized to .pr.
    /// </summary>
    [JsonIgnore]
    public string? PriorText { get; set; }

    /// <summary>The step is cached: its text is the prior build's, word for word, and it holds that
    /// build's code (goal.Merge). A cached step is not asked again — not of the decider, not of the
    /// LLM — and its code stands. Build-time only.</summary>
    [JsonIgnore]
    public bool IsCached => PriorText != null && PriorText == Text && Code.Count > 0;

    // an action call at the start of the text: module.action(
    private static readonly System.Text.RegularExpressions.Regex FormalHead = new(@"^[a-z]+\.[A-Za-z_]+\(");

    /// <summary>The step is written in formal — its text is its code. It is read as written, with every
    /// check an answer's line gets, and asked of no one: not the decider, not the LLM. Build-time only.</summary>
    [JsonIgnore]
    public bool IsFormal => FormalHead.IsMatch(Text);

    /// <summary>The step needs no answer: it is cached, or its own text is its code. Build-time only.</summary>
    [JsonIgnore]
    public bool IsAnswered => IsCached || IsFormal;

    /// <summary>Where the step is written: its line in the .goal file and its indent.</summary>
    [Store, LlmBuilder, Debug, Default]
    public global::app.goal.step.line.@this Line { get; internal set; } = new();

    [Store, LlmBuilder, Debug, Default]
    public string? Comment { get; internal set; }

    private global::app.goal.step.action.list.@this _code = new();
    /// <summary>The step's code: the actions it runs, as the .pr holds them under <c>code</c>.</summary>
    [Store, Debug, Default]
    public global::app.goal.step.action.list.@this Code
    {
        // A plain slot. Every action in it was born knowing this step — the reader constructs the
        // step shell first and hands it down, so there is nothing to repair on read.
        get => _code;
        set => _code = value ?? new();
    }

    private global::app.goal.step.pick.list.@this? _pick;
    /// <summary>The decider's reading of this step — its answers and the picks they mean. Build-time
    /// only: not stored in the .pr.</summary>
    [JsonIgnore]
    public global::app.goal.step.pick.list.@this Pick => _pick ??= new(this);

    /// <summary>
    /// Computed hash of the step text. Used by Setup for idempotency tracking.
    /// </summary>
    [JsonIgnore]
    public string? Hash => string.IsNullOrEmpty(Text) ? null
        : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Text))).ToLowerInvariant();

    [Store, LlmBuilder, Debug, Default]
    public string? Intent { get; internal set; }

    /// <summary>Tag set by enrichResponse: "known" (prior text matched), "hint" (text changed, prior available), or "new".</summary>
    [Store, Debug, Default]
    public string? Source { get; set; }

    [Debug]
    public global::app.warning.list.@this Warning { get; init; } = new();

    [Store, Debug, Default]
    public bool WaitForExecution { get; internal set; } = true;

    /// <summary>The goal this step belongs to — a BIRTH FACT. The reader constructs the goal shell
    /// first and hands it to each step at construction, so this is set once and never reassigned.
    /// <c>init</c> is the enforcement: there is no stamping it in afterwards, and no repair getter.</summary>
    [JsonIgnore]
    public global::app.goal.@this Goal { get; init; } = null!;
    /// <summary>A step starts through the step type's events, then its own.</summary>
    protected internal override global::app.type.item.@this? Level(int depth, actor.context.@this context) => depth switch
    {
        0 => context.App.step,
        1 => this,
        _ => null,
    };

    /// <summary>
    /// Starts this step through its <c>on.start</c>: what is bound before it, then its actions, then what is
    /// bound after it (<see cref="Level"/>). A before that fails or cancels is the result: the actions don't
    /// start, every after still runs on it. Error handling, caching, and timeouts are per-action modifiers, not
    /// step-level.
    /// </summary>
    public async Task<data.@this> Start(actor.context.@this context)
    {
        // The step's own frame spans its whole run — what is bound before and after it, and its actions — so the
        // step in play is this one throughout, and back to the caller's the moment it ends.
        global::app.callstack.call.@this frame;
        try { frame = context.CallStack.Push(this); }
        catch (global::app.error.CallStackOverflowException ex)
        {
            return context.Error(context.CallStack.Overflow(ex, Goal, this));
        }
        await using var _frame = frame;

        var answer = await on.start.Before(this, context);
        data.@this result;
        if (answer is { Success: false } or { Handled: true }) result = answer;
        else
        {
            try
            {
                result = await Code.Start(context);   // action.list owns the chain loop + fire
            }
            // a program's error that travelled as an exception is the step's answer, whole
            catch (global::app.error.AppException ex) { result = context.Error(ex.Error); }
            catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or OperationCanceledException))
            {
                // an exception plang didn't raise has no program key: the same one the action's catch gives it
                result = context.Error(new global::app.error.ServiceError(ex.Message, "ServiceError", 500) { Exception = ex });
            }
        }
        result = await on.start.After(this, result, context);
        return result;
    }


    /// <summary>
    /// Merges LLM-derived fields from another step onto this step.
    /// Structural fields (Text, Index, Line) are untouched.
    /// </summary>
    public void Merge(Step from)
    {
        if (from.Code.Count > 0)
            _code = from.Code;   // take the node — from is discarded; the graph is read-only after load

        if (from.Warning.Count > 0)
        {
            Warning.Clear();
            Warning.AddRange(from.Warning);
        }
    }

    public override string ToString() => $"[{Index}] {Text}";
}
