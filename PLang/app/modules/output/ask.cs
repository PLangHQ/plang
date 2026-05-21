using app.variables;

namespace app.modules.output;

/// <summary>
/// Payload marker for an in-flight ask. The question text rides as
/// <c>Data.Value</c>; the Snapshot rides as <c>Data.Snapshot</c>. Stage 2a.4
/// wires this in via <see cref="app.channels.channel.@this.Ask"/> on the
/// stateless Message channel. <see cref="global::app.IExitsGoal"/> makes the
/// step loop short-circuit when an action returns <c>Data&lt;Ask&gt;</c>.
/// </summary>
[global::app.Attributes.PlangType("ask")]
public sealed class Ask : global::app.IExitsGoal { }

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
[ModuleDescription("Ask the actor a question via the input channel. Stateful channels (Stream, goal-channel) answer synchronously; stateless channels (Message) return Data<Ask> + Snapshot so the engine can serialise and resume after the user answers.")]
[System.ComponentModel.Description("Ask the input channel a question. Stateful: synchronous answer. Stateless: Data<Ask> + Snapshot for suspend/resume.")]
[Example("ask user 'what's your name?', write to %name%",
    "output.ask Question([string] what's your name?) | variable.set Name([string] %name%), Value([object] %!data%)")]
[Example("output.ask question='Allow access? (y/n/a)', write to %answer%",
    "output.ask Question([string] Allow access? (y/n/a)) | variable.set Name([string] %answer%), Value([object] %!data%)")]
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

    public async Task<data.@this> Run()
    {
        // Resume path: channel pre-bound the answer under !ask.answer.
        var answer = Context.Variables.Get(AnswerVariableName);
        if (answer != null && answer.IsInitialized)
        {
            // The sentinel rides as the "answer" property of the infra root
            // variable "!ask". Variables.Remove only takes flat keys; removing
            // the root consumes the marker. "!ask" is reserved for this use.
            Context.Variables.Remove("!ask");
            return global::app.data.@this.Ok(answer.Value);
        }

        // Fresh path: delegate to the input channel. Stream blocks; Message
        // returns Data<Ask> with a Snapshot — the engine short-circuits and
        // the channel decides whether to materialise in-process or on the wire.
        var input = Context.Actor?.Channels.Resolve(global::app.channels.@this.Input)
            ?? throw new InvalidOperationException("No input channel registered on actor");
        return await input.Ask(this);
    }
}
