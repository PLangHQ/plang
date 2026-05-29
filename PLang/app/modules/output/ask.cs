using app.variables;

namespace app.modules.output;

/// <summary>
/// Payload for an in-flight or resolved ask. The Snapshot rides as
/// <c>Data.Snapshot</c>; the stateless Message channel populates it for
/// resume.
///
/// <para>Two states ride the same record:</para>
/// <list type="bullet">
///   <item><b>Suspend</b> — <see cref="Answer"/> is null. <see cref="ShouldExit"/>
///         returns true, so the step loop short-circuits.</item>
///   <item><b>Resolved</b> — <see cref="Answer"/> carries the user's response.
///         <see cref="ShouldExit"/> returns false; the step loop continues and the
///         trailing variable.set binds the Ask. Callers read <c>%name.Answer%</c>.</item>
/// </list>
/// </summary>
[global::app.Attributes.PlangType]
public sealed class Ask : global::app.IExitsGoal
{
    /// <summary>The user's response on the resume path. Null while the ask is
    /// pending — short-circuit semantics fire until this is bound.</summary>
    [Out] public string? Answer { get; init; }

    /// <inheritdoc/>
    public bool ShouldExit() => Answer == null;

    /// <summary>
    /// Renders the bound answer for PLang string contexts — `write to %name%`
    /// followed by `%name% equals "Alice"` compares text against the answer
    /// without needing `%name.Answer%`. Returns empty string for a pending Ask.
    /// **Note:** this means ToString() leaks the user's answer; do not use an
    /// `Ask` value in diagnostic / log paths. Output-channel routing is the
    /// right path; arbitrary string interpolation in trace dumps is not.
    /// </summary>
    public override string ToString() => Answer ?? string.Empty;
}

/// <summary>
/// Asks the actor a question via the input channel. Two paths:
///  - **Stateful channel** (Stream, in-process goal channel): the channel
///    answers synchronously; the answer flows back through Data.Value.
///  - **Stateless channel** (Message / HTTP, when wired): the channel returns
///    a <c>Data&lt;Ask&gt;</c> with Snapshot attached. The engine short-circuits
///    via the step-loop's ShouldExit. Resume re-runs the goal; the channel
///    pre-binds the answer under <c>%!ask.answer%</c> so the second call to
///    output.ask short-circuits to that value.
///
/// PLang: <c>- ask user "what's your name?", write to %name%</c>
/// </summary>
[Action("ask", Cacheable = false)]
public partial class ask : IContext
{
    /// <summary>The question text shown to the user.</summary>
    [IsNotNull]
    public partial data.@this<string> Question { get; init; }

    /// <summary>
    /// Names of variables whose current values survive into the suspend (per
    /// <c>vars:</c> annotation). Empty list = no extra state crosses the suspend.
    /// </summary>
    public partial data.@this? Variables { get; init; }

    /// <summary>Resume sentinel — variable name used to inject the answer.</summary>
    public const string AnswerVariableName = "!ask.answer";

    public async Task<data.@this<Ask>> Run()
    {
        // Resume path: channel pre-bound the answer under !ask.answer.
        var answer = Context.Variables.Get(AnswerVariableName);
        if (answer != null && answer.IsInitialized)
        {
            // The sentinel rides as the "answer" property of the infra root
            // variable "!ask". Variables.Remove only takes flat keys; removing
            // the root consumes the marker. "!ask" is reserved for this use.
            Context.Variables.Remove("!ask");
            return data.@this<Ask>.Ok(new Ask { Answer = answer.Value?.ToString() });
        }

        // Fresh path: delegate to the input channel. Stream blocks and returns
        // the user's typed answer as a string Data; Message returns a suspending
        // Ask with Snapshot attached. Coerce the wire shape into the Data<Ask>
        // contract here so callers never see the legacy string-bearing form.
        var input = Context.Actor?.Channels.Resolve(global::app.channels.@this.Input)
            ?? throw new InvalidOperationException("No input channel registered on actor");
        var askResult = await input.AskAsync(this);
        if (!askResult.Success) return data.@this<Ask>.From(askResult);
        // Stream-channel shape: a bare string answer. Lift into a resolved Ask
        // (no Snapshot needed — the answer is already here).
        if (askResult.Value is not Ask ask)
            return data.@this<Ask>.Ok(new Ask { Answer = askResult.Value?.ToString() });
        // Stateless-channel shape: a suspending Ask plus a Snapshot. Forward
        // Snapshot + Type so the engine's ShouldExit and the channel's resume
        // path both still trigger.
        return data.@this<Ask>.From(askResult);
    }
}
