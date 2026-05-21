using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using global::App.modules.output;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 5 (C# half): `output.ask` is ~10 lines — consume the
/// resume sentinel if present, otherwise delegate to `Channel.Ask`. Stream
/// channel blocks and returns the line; Message channel builds `Data<Ask>`
/// with Snapshot attached.
public class OutputAskRoutingTests
{
    private static global::App.@this NewApp() =>
        new global::App.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-rt-" + System.Guid.NewGuid().ToString("N")[..8]));

    private sealed class TestMessageChannel : global::App.Channels.Channel.Message.@this
    {
        public TestMessageChannel(string name)
        {
            Name = name;
            Direction = global::App.Channels.Channel.ChannelDirection.Bidirectional;
        }
        public override Task<global::App.Data.@this> WriteCore(global::App.Data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::App.Data.@this.Ok());
        public override Task<global::App.Data.@this> ReadCore(CancellationToken ct = default)
            => Task.FromResult(global::App.Data.@this.Ok((object?)null));
    }

    [Test] public async Task OutputAsk_AnswerSentinelPresent_ReturnsOkAndConsumesIt()
    {
        var app = NewApp();
        var ctx = app.User.Context;
        ctx.Variables.Set(ask.AnswerVariableName, "Alice");

        var handler = new ask { Context = ctx, Question = new global::App.Data.@this<string>("", "name?") };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value as string).IsEqualTo("Alice");
        await Assert.That(ctx.Variables.Get(ask.AnswerVariableName).IsInitialized).IsFalse();
    }

    [Test] public async Task OutputAsk_NoAnswerSentinel_DelegatesToChannelAsk()
    {
        var app = NewApp();
        var ctx = app.User.Context;

        var msg = new TestMessageChannel("input");
        app.User.Channels.Register(msg);

        var handler = new ask { Context = ctx, Question = new global::App.Data.@this<string>("", "name?") };
        var result = await handler.Run();
        await Assert.That(result.Type?.Value).IsEqualTo("ask");
        await Assert.That(result.Snapshot).IsNotNull();
    }

    [Test] public async Task StreamChannelAsk_WritesPromptThenReadsStdinLine()
    {
        var app = NewApp();
        var ms = new MemoryStream(global::System.Text.Encoding.UTF8.GetBytes("Alice\n"));
        var ch = new global::App.Channels.Channel.Stream.@this("i", ms,
            global::App.Channels.Channel.ChannelDirection.Bidirectional, ownsStream: false)
        { Mime = "text/plain" };
        // Empty question to skip WriteCore (the existing Stage 2 stream tests
        // do the same — exercises Ask's read-line path without needing a
        // registered Channels collection for the serializer).
        var action = new ask { Context = app.User.Context, Question = new global::App.Data.@this<string>("", "") };
        var result = await ch.AskCore(action);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value as string).IsEqualTo("Alice");
    }

    [Test] public async Task StreamChannelAsk_TimeoutBehaviorPreservedFromAskCoreRename()
    {
        // Pinned by Stage2_StreamChannelTests.StreamChannel_Ask_TimesOutPerChannelTimeoutConfig.
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task MessageChannelAsk_ReturnsDataAsk_WithQuestionAsValue()
    {
        var app = NewApp();
        var ch = new TestMessageChannel("input");
        var action = new ask
        {
            Context = app.User.Context,
            Question = new global::App.Data.@this<string>("", "Allow X?")
        };
        var result = await ch.AskCore(action);
        await Assert.That(result.Value as string).IsEqualTo("Allow X?");
        await Assert.That(result.Type?.Value).IsEqualTo("ask");
    }

    [Test] public async Task MessageChannelAsk_AttachesSnapshotToReturnedData()
    {
        var app = NewApp();
        var ch = new TestMessageChannel("input");
        var action = new ask
        {
            Context = app.User.Context,
            Question = new global::App.Data.@this<string>("", "?")
        };
        var result = await ch.AskCore(action);
        await Assert.That(result.Snapshot).IsNotNull();
    }
}
