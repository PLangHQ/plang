using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Grant = global::app.type.item.permission.@this;
using Verb = global::app.type.item.permission.Verb;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// A consent's answer is what really happened at the input: nobody able to answer (a cancel, a closed or broken
/// stream, an input that says no one is there) is a "no"; a bug inside the ask is that bug, never a "no".
public class ConsentAskTests : System.IAsyncDisposable
{
    private readonly global::app.@this app;

    public ConsentAskTests()
        => app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-consentask-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // the actor asked, its input throwing what it's given; a yes would answer Ok
    private async Task<global::app.data.@this> Asked(System.Exception thrown)
    {
        var user = app.actor.list.User;
        user.Channel.Register(new Throwing(user.Context, thrown));
        var request = Grant.Request(user, "/elsewhere", Verb.read);
        return await user.Permission.Ask("Allow it? (y/n)", request, user.Context, _ => Task.FromResult(user.Context.Ok()));
    }

    [Test]
    public async Task ABugInTheAsk_IsNotADenial()
    {
        var answer = await Asked(new System.InvalidOperationException("the ask's own bug"));
        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsNotEqualTo("PermissionDenied");
        await Assert.That(answer.Error.Message).Contains("InvalidOperationException");
    }

    [Test]
    public async Task ANullReferenceInTheAsk_IsNotADenial()
    {
        var answer = await Asked(new System.NullReferenceException());
        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsNotEqualTo("PermissionDenied");
    }

    [Test]
    public async Task ACancelledInput_IsADenial()
        => await Denied(await Asked(new System.OperationCanceledException()));

    [Test]
    public async Task ABrokenInput_IsADenial()
        => await Denied(await Asked(new System.IO.IOException("broken pipe")));

    [Test]
    public async Task AClosedInput_IsADenial()
        => await Denied(await Asked(new System.ObjectDisposedException("input")));

    [Test]
    public async Task AnInputThatSaysNobodyAnswers_IsADenial()
        => await Denied(await Asked(new global::app.error.AppException(new global::app.error.NoAnswer("no one is there"))));

    private static async Task Denied(global::app.data.@this answer)
    {
        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsEqualTo("PermissionDenied");
    }

    private sealed class Throwing : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx; private readonly System.Exception _thrown;
        public Throwing(global::app.actor.context.@this ctx, System.Exception thrown)
        { _ctx = ctx; _thrown = thrown; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default) => throw _thrown;
    }
}
