using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// The actor's consent is its own answer, from its input: a step can't answer the gate's question for it. A step that
/// sets %!ask.answer% (the resume sentinel a stateless channel pre-binds) is still asked, on its input — a closed
/// input answers no — for an out-of-root read, an identity's export, a bundled copy.
public class AskAnswerTests : System.IAsyncDisposable
{
    private readonly string outside;
    private readonly global::app.@this app;
    private readonly Counting channel;
    private readonly global::app.actor.context.@this user;

    public AskAnswerTests()
    {
        outside = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-askanswer-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        System.IO.File.WriteAllText(System.IO.Path.Combine(outside, "secret.txt"), "outside");
        app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-askanswer-root-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        user = app.actor.list.User.Context;
        channel = new Counting(user);
        app.actor.list.User.Channel.Register(channel);
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // An input that answers no, counting the asks.
    private sealed class Counting : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public int Count { get; private set; }
        public Counting(global::app.actor.context.@this ctx) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Count++;
            return Task.FromResult(action.Context.Ok("n"));
        }
    }

    private async Task Answering() => await (await (await user.Action("variable.set(Name=\"!ask.answer\", Value=\"a\")")).Start(user)).IsSuccess();

    private async Task Refused(global::app.data.@this answer)
    {
        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsEqualTo("PermissionDenied");
        await Assert.That(channel.Count).IsEqualTo(1);
    }

    // a binding before the input's ask that answers "a" in the channel's place — what a step's `on ask on "input"`
    // binding does — never answers a consent: it may watch it, never answer it
    private void Binding()
        => app.actor.list.User.Context.on.Bind(channel, "ask", global::app.@event.When.before, (item, data, ctx) =>
        {
            var answer = ctx.Ok("a");
            answer.Handled = true;
            return Task.FromResult(answer);
        }, global::app.@event.binding.Scope.actor);

    [Test] public async Task ABindingOnTheInputsAsk_DoesNotAnswerARead()
    {
        Binding();
        await Refused(await user.File(System.IO.Path.Combine(outside, "secret.txt")).Read(user));
    }

    [Test] public async Task ABindingOnTheInputsAsk_DoesNotAnswerAnExport()
    {
        Binding();
        await Refused(await (await user.Action("identity.export()")).Start(user));
    }

    [Test] public async Task AStepSettingTheAskAnswer_IsStillAskedForARead()
    {
        await Answering();
        await Refused(await user.File(System.IO.Path.Combine(outside, "secret.txt")).Read(user));
    }

    [Test] public async Task AStepSettingTheAskAnswer_IsStillAskedForAnExport()
    {
        await Answering();
        await Refused(await (await user.Action("identity.export()")).Start(user));
    }

    [Test] public async Task AStepSettingTheAskAnswer_IsStillAskedForACopy()
    {
        await Answering();
        var into = System.IO.Path.Combine(outside, "copied");
        await Refused(await user.File(System.IO.Path.Combine(outside, "secret.txt")).CopyTo(user.File(into), overwrite: true, subfolder: true, user));
        await Assert.That(System.IO.File.Exists(into)).IsFalse();
    }
}
