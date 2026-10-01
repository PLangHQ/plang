namespace app.module.output;

/// <summary>
/// A pending ask — the question went out and its answer comes later: the goal suspends on it (an
/// <see cref="global::app.IExitsGoal"/>, so the step loop short-circuits), and the snapshot on its Data resumes the
/// goal once the user replies (a message channel). An answered ask is never an Ask: it is the answer itself.
/// </summary>
[global::app.Attributes.PlangType("ask")]
public sealed class Ask : global::app.type.item.@this, global::app.type.item.ICreate<Ask>, global::app.IExitsGoal
{
    /// <summary>The entity is "ask" (the namespace-tail default would say
    /// "output", the owning module, not this value's name).</summary>
    protected internal override global::app.type.@this Type
        => new("ask", typeof(Ask));

    /// <summary>What is asked, as the asker resolved it — what a program reading this plang shows the person.</summary>
    [global::app.Out] public global::app.type.item.text.@this? Question { get; init; }
}

/// <summary>
/// Asks the actor a question via the input channel, and answers with what the user answered — the user's data
/// itself (a line typed is text: <c>- ask "what is your name?", write to %name%</c> → <c>%name%</c> is "Ada").
/// Two paths:
///  - **Stateful channel** (Stream, in-process goal channel): the channel answers at once; the answer is the
///    step's result as it came.
///  - **Stateless channel** (Message / HTTP, when wired): the channel answers a pending <see cref="Ask"/> with
///    its Snapshot; the step loop suspends on it. Resume re-runs the goal; the channel pre-binds the answer under
///    <c>%!ask.answer%</c>, so the second call to output.ask answers with that, as it is.
///
/// PLang: <c>- ask user "what's your name?", write to %name%</c>
/// </summary>
[Action("ask", Cacheable = false)]
public partial class ask : IContext
{
    /// <summary>The question text shown to the user.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> Question { get; init; }

    /// <summary>
    /// Names of variables whose current values survive into the suspend (per
    /// <c>vars:</c> annotation). Empty list = no extra state crosses the suspend.
    /// </summary>
    public partial data.@this? Variable { get; init; }

    /// <summary>Resume sentinel — variable name used to inject the answer.</summary>
    public const string AnswerVariableName = "!ask.answer";

    // The answer is whatever the user's data is — relayed, never re-made: a bare Data, not Data<T>.
    public async Task<data.@this> Start()
    {
        // Resume path: channel pre-bound the answer under !ask.answer.
        var answer = await new global::app.type.item.variable.@this(AnswerVariableName).Start(Context);
        if (answer.IsInitialized)
        {
            // The sentinel rides as the "answer" property of the infra root
            // variable "!ask". Variable.Remove only takes flat keys; removing
            // the root consumes the marker. "!ask" is reserved for this use.
            await Context.Variable.Remove("!ask");
            return answer;
        }

        // Fresh path: the input channel answers — the user's data (a stream's line, a goal's result), or a pending
        // Ask with a Snapshot (a message channel), which the step loop's ShouldExit suspends on.
        // none there: the actor has no input, or its input is a goal channel running its own body
        if (Context.Actor?.Channel.Get(global::app.channel.list.@this.Input) is not { } input)
            return Context.Error(new global::app.error.Error(
                "there is no input channel to ask on — none is registered, or it is busy running its own goal",
                "NoInputChannel", 400));
        // The wait ends when the run's cancellation says so — the program's timeout on the ask, a test's, Ctrl-C.
        return await input.AskAsync(this, Context.CancellationToken);
    }
}
