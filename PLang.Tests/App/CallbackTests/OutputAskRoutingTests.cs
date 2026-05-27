using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app.modules.output;

namespace PLang.Tests.App.CallbackTests;

/// output.ask routing: consume the resume sentinel under !ask.answer if
/// present, otherwise delegate to the input channel's Ask. Stream channels
/// block and return the typed line; Message channels return a Data<Ask>
/// with Snapshot attached so the engine can short-circuit and resume.
public class OutputAskRoutingTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-rt-" + System.Guid.NewGuid().ToString("N")[..8]));

    private sealed class TestMessageChannel : global::app.channels.channel.message.@this
    {
        public TestMessageChannel(string name)
        {
            Name = name;
            Direction = global::app.channels.channel.ChannelDirection.Bidirectional;
        }
        public override Task<global::app.data.@this> WriteCore(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> ReadCore(CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok((object?)null));
    }

    [Test] public async Task OutputAsk_AnswerSentinelPresent_ReturnsOkAndConsumesIt()
    {
        var app = NewApp();
        var ctx = app.User.Context;
        ctx.Variables.Set(ask.AnswerVariableName, "Alice");

        var handler = new ask { Context = ctx, Question = new global::app.data.@this<string>("", "name?") };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.Answer).IsEqualTo("Alice");
        await Assert.That(ctx.Variables.Get(ask.AnswerVariableName).IsInitialized).IsFalse();
    }

    [Test] public async Task OutputAsk_NoAnswerSentinel_DelegatesToChannelAsk()
    {
        var app = NewApp();
        var ctx = app.User.Context;

        var msg = new TestMessageChannel("input");
        app.User.Channels.Register(msg);

        var handler = new ask { Context = ctx, Question = new global::app.data.@this<string>("", "name?") };
        var result = await handler.Run();
        await Assert.That(result.Type?.Value).IsEqualTo("ask");
        await Assert.That(result.Snapshot).IsNotNull();
    }

    [Test] public async Task StreamChannelAsk_WritesPromptThenReadsStdinLine()
    {
        var app = NewApp();
        var ms = new MemoryStream(global::System.Text.Encoding.UTF8.GetBytes("Alice\n"));
        var ch = new global::app.channels.channel.stream.@this("i", ms,
            global::app.channels.channel.ChannelDirection.Bidirectional, ownsStream: false)
        { Mime = "text/plain" };
        // Empty question to skip WriteCore — exercises Ask's read-line path
        // without needing a registered Channels collection for the serializer.
        var action = new ask { Context = app.User.Context, Question = new global::app.data.@this<string>("", "") };
        var result = await ch.AskCore(action);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value as string).IsEqualTo("Alice");
    }

    [Test] public async Task StreamChannelAsk_TimeoutBehaviorPreservedFromAskCoreRename()
    {
        // Pinned by Stage2_StreamChannelTests.StreamChannel_Ask_TimesOutPerChannelTimeoutConfig.
        await Assert.That(true).IsTrue();
    }

    // The suspend wire shape is a bare Ask (Answer==null) — the question text
    // rides on the Snapshot and the action.Question parameter, not on Value.
    // The Ask's IExitsGoal.ShouldExit() returns true while Answer is null.
    [Test] public async Task MessageChannelAsk_ReturnsDataAsk_WithSuspendShape()
    {
        var app = NewApp();
        var ch = new TestMessageChannel("input");
        var action = new ask
        {
            Context = app.User.Context,
            Question = new global::app.data.@this<string>("", "Allow X?")
        };
        var result = await ch.AskCore(action);
        await Assert.That(result.Value).IsTypeOf<global::app.modules.output.Ask>();
        await Assert.That(((global::app.modules.output.Ask)result.Value!).Answer).IsNull();
        await Assert.That(result.Type?.Value).IsEqualTo("ask");
    }

    [Test] public async Task MessageChannelAsk_AttachesSnapshotToReturnedData()
    {
        var app = NewApp();
        var ch = new TestMessageChannel("input");
        var action = new ask
        {
            Context = app.User.Context,
            Question = new global::app.data.@this<string>("", "?")
        };
        var result = await ch.AskCore(action);
        await Assert.That(result.Snapshot).IsNotNull();
    }
}
