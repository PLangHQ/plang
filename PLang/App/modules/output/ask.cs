using App.Variables;

namespace App.modules.output;

/// <summary>
/// Payload marker for an in-flight ask. The question text rides as
/// <c>Data.Value</c>; the Snapshot rides as <c>Data.Snapshot</c>. Stage 2a.4
/// wires this in via <see cref="App.Channels.Channel.@this.Ask"/> on the
/// stateless Message channel. <see cref="global::App.IExitsGoal"/> makes the
/// step loop short-circuit when an action returns <c>Data&lt;Ask&gt;</c>.
/// </summary>
[global::App.Attributes.PlangType("ask")]
public sealed class Ask : global::App.IExitsGoal { }

/// <summary>
/// Asks the actor a question. Two modes:
///  - **Fresh:** captures the current Position + the variables named by <c>vars:</c>
///    + the actor name into an <see cref="App.Callback.AskCallback"/>, returns it as
///    Data&lt;AskCallback&gt;. The caller (HTTP channel etc.) serialises and suspends
///    the goal until the user answers.
///  - **Resumed:** when <see cref="App.Callback.AskCallback.Run"/> re-dispatches this
///    action, it pre-binds the answer under <c>%!ask.answer%</c>. The handler detects
///    the marker, returns the answer, and lets the calling step write it to its
///    <c>write to %x%</c> target. No fresh ask is issued.
///
/// PLang: <c>- ask user "what's your name?", vars: %userId%, write to %name%</c>
/// </summary>
[ModuleDescription("Ask the actor a question — issues an AskCallback when no answer is in scope yet.")]
[System.ComponentModel.Description("Ask a question; returns Data<AskCallback> on first call and the bound answer on resume")]
[Action("ask", Cacheable = false)]
public partial class ask : IContext
{
    /// <summary>The question text shown to the user.</summary>
    [IsNotNull]
    public partial Data.@this<string> Question { get; init; }

    /// <summary>
    /// Names of variables whose current values survive into the AskCallback (per
    /// <c>vars:</c> annotation). Empty list = no extra state crosses the suspend.
    /// </summary>
    public partial Data.@this? Variables { get; init; }

    /// <summary>Resume sentinel — variable name used by AskCallback.Run to inject the answer.</summary>
    public const string AnswerVariableName = "!ask.answer";

    public async Task<Data.@this> Run()
    {
        // Resume path: callback.run / channel pre-bound the answer under !ask.answer.
        var answer = Context.Variables.Get(AnswerVariableName);
        if (answer != null && answer.IsInitialized)
        {
            // The sentinel rides as the "answer" property of the infra root
            // variable "!ask". Variables.Remove only takes flat keys; removing
            // the root consumes the marker. "!ask" is reserved for this use.
            Context.Variables.Remove("!ask");
            return global::App.Data.@this.Ok(answer.Value);
        }

        // Fresh path: delegate to the input channel. Stream blocks; Message
        // returns Data<Ask> with a Snapshot — the engine short-circuits and
        // the channel decides whether to materialise in-process or on the wire.
        var input = Context.Actor?.Channels.Resolve(global::App.Channels.@this.Input)
            ?? throw new InvalidOperationException("No input channel registered on actor");
        return await input.Ask(this);
    }
}
