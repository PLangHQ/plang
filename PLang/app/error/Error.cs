using app.actor.context;
using Goal = app.goal.@this;
using Call = app.callstack.call.@this;
using Action = app.goal.step.action.@this;

namespace app.error;

/// <summary>
/// An error — the one error type; its kinds (ServiceError, ActionError, …) derive from it.
/// Captures execution context (step, goal) when provided.
///
/// <para>An error IS a plang value (<c>item</c>): when it rides as a VALUE — the
/// <c>%!error%</c> view, the errors-trail, a re-raised <c>throw %!error%</c>, a
/// snapshot capturing an error — it renders and navigates itself, with no
/// wrapper and no <c>clr</c>/<c>TypedValueNode</c> carrier. Its members
/// (<c>Message</c>, <c>Key</c>, <c>Details</c>, the causing <c>list</c>) navigate
/// directly. The value-face is independent of <c>Data.Error</c>, the sidecar
/// failure channel on the envelope.</para>
/// </summary>
[global::app.Attributes.PlangType]
public class Error : global::app.type.item.@this
{
    public string Id { get; }
    public string Message { get; }
    public string Key { get; }
    public int StatusCode { get; }
    public string? FixSuggestion { get; init; }
    public string? HelpfulLinks { get; init; }
    public DateTime CreatedUtc { get; }
    public Exception? Exception { get; init; }
    /// <summary>
    /// Arbitrary structured context about the failure. Providers attach things
    /// like raw LLM responses, HTTP status bodies, parse positions, etc. Exposed
    /// to PLang goals via %!error.Details.KEY% so error handlers can report
    /// rich context without the provider having to extend the error class.
    /// </summary>
    public Dictionary<string, object?>? Details { get; set; }
    /// <summary>
    /// Snapshot of the parameters as they arrived at the failing handler — the .pr
    /// raw value/type and the resolved final value for each. Populated by the
    /// source-generated ExecuteAsync whenever a handler returns an error. Lets you
    /// see "this is what the handler saw" without re-running with a debug flag.
    /// </summary>
    public List<ParamSnapshot>? Params { get; set; }

    /// <summary>
    /// Typed value(s) attached to the failure — the thing thrown
    /// (<c>- throw %order%, %item%</c>) or a payload a handler chooses to carry.
    /// Held as the intact <see cref="data.@this"/> (a plang <c>list</c>) so the values
    /// keep their type and render/navigate in full (<c>%!error.data%</c>,
    /// <c>%!error.data[0]%</c>) instead of being flattened to a string. Distinct from
    /// <see cref="Message"/> (the human line) and <see cref="Details"/> (provider
    /// diagnostic context).
    /// </summary>
    public global::app.data.@this<global::app.type.item.list.@this>? Data { get; init; }

    /// <summary>The errors that CAUSED this one — empty when nothing did. "file.read is not valid"
    /// holds the missing parameters that made it so; an error raised while handling another holds
    /// that one. Never null, so a reader never guards before walking it. <c>init</c> so a producer
    /// can hand the causes in whole: <c>new ActionError(…) { Action = this, list = causes }</c>.</summary>
    public List<Error> list { get; init; } = new();

    /// <summary>The error renders itself — its flattened wire shape, written straight
    /// to the wire (no intermediate value). $type discriminates the subtype; the
    /// recursive causing list lets each nested error write itself. The live
    /// back-references that can't round-trip (Exception, Step, Goal, CallFrames) are
    /// dropped — the snapshot's CallStack section carries the chain. Symmetric with the
    /// read side (<c>ErrorWire</c>).</summary>
    public override void Write(global::app.channel.serializer.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("$type");       writer.String(GetType().Name);
        writer.Name("id");          writer.String(Id);
        writer.Name("message");     writer.String(Message);
        writer.Name("key");         writer.String(Key);
        writer.Name("statusCode");  writer.Int(StatusCode);
        writer.Name("createdUtc");  writer.DateTime(CreatedUtc);
        if (FixSuggestion != null) { writer.Name("fixSuggestion"); writer.String(FixSuggestion); }
        if (HelpfulLinks != null)  { writer.Name("helpfulLinks");  writer.String(HelpfulLinks); }
        WriteSpecific(writer);
        if (list is { Count: > 0 })
        {
            writer.Name("list");
            writer.BeginArray(list.Count);
            foreach (var c in list)
                ((global::app.type.item.@this)c).Write(writer); // each error is an item — it writes itself
            writer.EndArray();
        }
        writer.EndObject();
    }

    /// <summary>Subtypes add their own wire fields here — written mid-object, after the
    /// common fields and before <c>errorChain</c>. Base errors have none.</summary>
    protected virtual void WriteSpecific(global::app.channel.serializer.IWriter writer) { }

    private global::app.data.@this<global::app.snapshot.@this>? _callback;

    /// <summary>
    /// PLang surface <c>%!error.callback%</c> resolves through here. First read invokes
    /// <c>app.Snapshot(this)</c> on the App the error's own <see cref="Context"/> belongs to — the
    /// THROW-TIME snapshot: CallStack from this error's <see cref="CallFrames"/> (the live stack has
    /// unwound past the failing action by handler time) and variables via <c>SnapshotAt(this)</c>.
    /// Wrapped directly as <c>Data&lt;Snapshot&gt;</c>; resume goes through
    /// <see cref="app.snapshot.@this.Resume"/>, same path as ask-resume. Cached per Error instance —
    /// reading twice returns the same Data. An error with no context was not raised inside a run:
    /// there is nothing to resume, so it has no callback — a <c>NoCallback</c> error, not a throw.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public global::app.data.@this<global::app.snapshot.@this> Callback
    {
        get
        {
            if (_callback != null) return _callback;
            if (Context == null)
                return global::app.data.@this<global::app.snapshot.@this>.FromError(new Error(
                    $"error '{Key}' was not raised inside a run, so it has no callback to resume.", "NoCallback", 400));
            var snap = Context.App.Snapshot(this, Context);
            _callback = Context.Ok<global::app.snapshot.@this>(snap);
            _callback.Snapshot = snap;
            return _callback;
        }
    }
    public Action? Action { get; set; }
    public Step? Step { get; set; }
    public Goal? Goal { get; set; }
    public IReadOnlyList<Call> CallFrames { get; set; } = Array.Empty<Call>();
    /// <summary>The variables as they were when the error happened — each variable's Data whole,
    /// keyed by name (<c>%!error.Variables.foo%</c>). Captured by assert, and by the frame for every
    /// error under --debug; null otherwise (variables can hold secrets).</summary>
    public global::app.type.item.dict.@this? Variables { get; set; }

    /// <summary>
    /// The execution context where this error occurred. Used by verbose debug to dump variables.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this? Context { get; set; }

    /// <summary>
    /// Creates an error with a message. Use for errors not tied to a specific execution context.
    /// </summary>
    public Error(string message, string key = "Error", int statusCode = 400)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        Message = message;
        Key = key;
        StatusCode = statusCode;
        CreatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Snapshot-restore ctor — reconstructs an Error from wire with its original
    /// <see cref="Id"/> and <see cref="CreatedUtc"/> preserved (both are otherwise
    /// set only at first construction). The live back-references (Step, Goal,
    /// CallFrames, Exception) are intentionally NOT restored: the CallStack
    /// section already carries the frame chain, and a live Exception / Goal
    /// object graph cannot round-trip. Used by <see cref="ErrorWire"/>.
    /// </summary>
    private Error(string id, string message, string key, int statusCode, DateTime createdUtc)
    {
        Id = id;
        Message = message;
        Key = key;
        StatusCode = statusCode;
        CreatedUtc = createdUtc;
    }

    /// <summary>
    /// Factory mirror of the snapshot-restore ctor that also re-applies the
    /// init-only advisory fields. See <see cref="ErrorWire"/>.
    /// </summary>
    internal static Error Restore(string id, string message, string key, int statusCode,
        DateTime createdUtc, string? fixSuggestion, string? helpfulLinks)
        => new Error(id, message, key, statusCode, createdUtc)
        {
            FixSuggestion = fixSuggestion,
            HelpfulLinks = helpfulLinks,
        };

    /// <summary>
    /// Creates an error tied to a specific step. Goal is inferred from step.Goal.
    /// </summary>
    public Error(string message, Step? step, string key = "Error", int statusCode = 400)
        : this(message, key, statusCode)
    {
        Step = step;
        Goal = step?.Goal;
    }

    /// <summary>
    /// Creates an error with a step and explicit Call chain snapshot.
    /// </summary>
    public Error(string message, Step? step, IReadOnlyList<Call> callFrames, string key = "Error", int statusCode = 400)
        : this(message, step, key, statusCode)
    {
        CallFrames = callFrames;
    }

    /// <summary>
    /// Creates an error from an execution context. Captures step, goal, and Call chain automatically.
    /// </summary>
    public Error(string message, actor.context.@this context, string key = "Error", int statusCode = 400)
        : this(message, context.Step, key, statusCode)
    {
        Goal = context.Goal;
        Context = context;
        CallFrames = context.CallStack.Current?.SnapshotChain() ?? (IReadOnlyList<Call>)Array.Empty<Call>();
    }

    /// <summary>
    /// Wraps a CLR exception as an Error. StatusCode defaults to 500 (runtime error).
    /// </summary>
    public static Error FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        // A keyed exception names its own error — "PrFormatOutdated", not a generic "Exception".
        if (ex is AppException keyed) (key, statusCode) = (keyed.Key, keyed.StatusCode);
        return new Error(ex.Message, key, statusCode)
        {
            Exception = ex
        };
    }

    /// <summary>
    /// Wraps a CLR exception as an Error with execution context for step/goal/callstack capture.
    /// </summary>
    public static Error FromException(Exception ex, actor.context.@this context, string key = "Exception", int statusCode = 500)
    {
        return new Error(ex.Message, context, key, statusCode)
        {
            Exception = ex
        };
    }

    /// <summary>The last-resort line, <c>[Key] Message</c>: for when the error cannot be shown by
    /// <c>/system/error/Show</c> (the show itself failed, or the app never started).</summary>
    public override string ToString() => $"[{Key}] {Message}";
}
