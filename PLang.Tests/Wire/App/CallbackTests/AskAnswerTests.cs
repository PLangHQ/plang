using app.module.output;

namespace PLang.Tests.App.CallbackTests;

/// <summary>
/// An answered ask is the user's data itself: <c>- ask "what is your name?", write to %name%</c> binds the text
/// "Ada", which renders and compares as the text it is. Only a pending ask is an Ask — the goal suspends on it and
/// resumes with the answer as it came. What a binding answers in the channel's place rides as it is too.
/// </summary>
public class AskAnswerTests
{
    private sealed class TestMessageChannel : global::app.channel.type.message.@this
    {
        public TestMessageChannel(string name)
        {
            Name = name;
            Direction = global::app.channel.ChannelDirection.Bidirectional;
        }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok((object?)null));
    }

    private static global::app.channel.type.stream.@this Console(global::app.@this app, string typed)
    {
        var input = new global::app.channel.type.stream.@this("input", new MemoryStream(System.Text.Encoding.UTF8.GetBytes(typed)),
            global::app.channel.ChannelDirection.Input, ownsStream: true) { Mime = "text/plain" };
        app.actor.list.User.Channel.Register(input);
        return input;
    }

    // `ask ""` — the question is empty so no output channel is needed; the answer is what the input gives.
    private static async Task<global::app.data.@this> Asked(global::app.actor.context.@this ctx)
        => await new global::app.goal.step.action.@this(new ask(ctx)
            { Question = new global::app.data.@this<global::app.type.item.text.@this>("", "", context: ctx) }, ctx).Start(ctx);

    [Test] public async Task ConsoleAsk_WritesTheTextItself()
    {
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false).Testing();
        var ctx = app.actor.list.User.Context;
        Console(app, "Ada\n");

        var asked = await Asked(ctx);
        await asked.IsSuccess();
        await ctx.Variable.Set("name", asked);

        var name = await ctx.Variable.Get("name");
        await Assert.That(name.Peek()).IsTypeOf<global::app.type.item.text.@this>();
        await Assert.That(name.Peek().ToString()).IsEqualTo("Ada");
    }

    [Test] public async Task TheAnswer_RendersInATemplate()
    {
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false).Testing();
        var ctx = app.actor.list.User.Context;
        Console(app, "Ada\n");
        await ctx.Variable.Set("name", await Asked(ctx));

        var greeting = (global::app.data.@this)Make.Template(ctx, "greeting", "Hello, %name%!").value!;
        await Assert.That((await greeting.Value())?.ToString()).IsEqualTo("Hello, Ada!");
    }

    [Test] public async Task TheAnswer_Compares()
    {
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false).Testing();
        var ctx = app.actor.list.User.Context;
        Console(app, "Ingi\n");
        await ctx.Variable.Set("name", await Asked(ctx));

        var equals = new global::app.data.Operator("==");
        var name = await ctx.Variable.Get("name");
        await Assert.That((await equals.Evaluate(name, ctx.Ok(new global::app.type.item.text.@this("Ingi")), ctx)).ToBoolean()).IsTrue();
        await Assert.That((await equals.Evaluate(name, ctx.Ok(new global::app.type.item.text.@this("Ada")), ctx)).ToBoolean()).IsFalse();
    }

    // A message channel suspends on a pending Ask; resumed, the ask answers with the answer as it came.
    [Test] public async Task MessageAsk_Suspends_ThenResumesWithTheAnswerAsIs()
    {
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false).Testing();
        var ctx = app.actor.list.User.Context;
        app.actor.list.User.Channel.Register(new TestMessageChannel("input"));

        var pending = await Asked(ctx);
        await Assert.That(pending.Peek()).IsTypeOf<Ask>();
        await Assert.That(pending.ShouldExit()).IsTrue();
        await Assert.That(pending.Snapshot).IsNotNull();

        await new global::app.type.item.variable.@this(ask.AnswerVariableName).Set(new global::app.type.item.text.@this("Ada"), ctx);
        var resumed = await Asked(ctx);
        await Assert.That(resumed.ShouldExit()).IsFalse();
        await Assert.That(resumed.Peek()).IsTypeOf<global::app.type.item.text.@this>();
        await Assert.That(resumed.Peek().ToString()).IsEqualTo("Ada");
    }

    // What a before-binding answers in the channel's place is the ask's answer, as it is.
    [Test] public async Task ABeforeBindingsAnswer_RidesAsItIs()
    {
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false).Testing();
        var ctx = app.actor.list.User.Context;
        var input = Console(app, "never read\n");
        var given = new global::app.type.item.text.@this("from the binding");
        input.Own().Bind("ask", global::app.@event.When.before, (_, _, c) =>
        {
            global::app.data.@this answered = c.Ok(given);
            answered.Handled = true;
            return Task.FromResult(answered);
        }, app.actor.list.User, global::app.@event.binding.Scope.actor);

        var asked = await Asked(ctx);
        await asked.IsSuccess();
        await Assert.That(asked.Peek()).IsSameReferenceAs(given);
    }
}
