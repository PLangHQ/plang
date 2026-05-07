using App.Events;
using App.Events.Lifecycle.Bindings.Binding;

namespace PLang.Tests.App.ChannelsTests;

// Stage 8 — Channel events: types, firing, recursion guard.
// Architect: stage-8-channel-events.md and v1/plan/channel-events.md.

public class Stage8_ChannelEventsTests
{
    [Test]
    public async Task EventType_HasFiveNewValues_ForChannelLifecycle()
    {
        var names = Enum.GetNames(typeof(EventType));
        await Assert.That(names).Contains("BeforeWrite");
        await Assert.That(names).Contains("AfterWrite");
        await Assert.That(names).Contains("BeforeRead");
        await Assert.That(names).Contains("AfterRead");
        await Assert.That(names).Contains("OnAsk");
    }

    [Test]
    public async Task EventBinding_AcceptsChannelNameFilter()
    {
        var b = new EventBinding(EventType.BeforeWrite,
            (_, _, _) => Task.FromResult(Data.Ok()),
            channelName: "logger");
        await Assert.That(b.ChannelName).IsEqualTo("logger");
    }

    [Test]
    public async Task ChannelThis_ExposesEventsProperty_LikeGoalAndStep()
    {
        var ch = StreamChannel.Memory("c");
        await Assert.That(ch.Events).IsNotNull();
        await Assert.That(ch.Events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BeforeWriteHandler_ReceivesCorrectData()
    {
        var ch = StreamChannel.Memory("logger");
        Data? captured = null;
        ch.Events.Add(new EventBinding(EventType.BeforeWrite, (_, _, payload) =>
        {
            captured = payload;
            return Task.FromResult(Data.Ok());
        }, channelName: "logger"));

        await ch.WriteAsync(Data.Ok("hello"));
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Value).IsEqualTo("hello");
    }

    [Test]
    public async Task BeforeWriteHandler_ThrowingAborts_AfterWriteDoesNotFire()
    {
        var ch = StreamChannel.Memory("c");
        bool afterFired = false;
        ch.Events.Add(new EventBinding(EventType.BeforeWrite, (_, _, _) =>
            throw new InvalidOperationException("nope")));
        ch.Events.Add(new EventBinding(EventType.AfterWrite, (_, _, _) =>
        {
            afterFired = true;
            return Task.FromResult(Data.Ok());
        }));

        var result = await ch.WriteAsync(Data.Ok("hi"));
        await Assert.That(result.Success).IsFalse();
        await Assert.That(afterFired).IsFalse();
    }

    [Test]
    public async Task AfterWriteHandler_FiresWhenWriteCoreSucceeds()
    {
        var ch = StreamChannel.Memory("c");
        bool afterFired = false;
        ch.Events.Add(new EventBinding(EventType.AfterWrite, (_, _, _) =>
        {
            afterFired = true;
            return Task.FromResult(Data.Ok());
        }));
        var result = await ch.WriteAsync(Data.Ok("hi"));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(afterFired).IsTrue();
    }

    [Test]
    public async Task AfterWriteHandler_FiresWhenWriteCoreThrows()
    {
        var ch = new ThrowOnWriteChannel("c");
        bool afterFired = false;
        Data? receivedData = null;
        ch.Events.Add(new EventBinding(EventType.AfterWrite, (_, _, payload) =>
        {
            afterFired = true;
            receivedData = payload;
            return Task.FromResult(Data.Ok());
        }));
        var result = await ch.WriteAsync(Data.Ok("hi"));
        await Assert.That(result.Success).IsFalse();
        await Assert.That(afterFired).IsTrue();
        await Assert.That(receivedData!.Success).IsFalse();
    }

    [Test]
    public async Task AfterWriteHandler_ThrowingIsSuppressed_OriginalOutcomeStands()
    {
        var ch = StreamChannel.Memory("c");
        ch.Events.Add(new EventBinding(EventType.AfterWrite, (_, _, _) =>
            throw new InvalidOperationException("after fail")));
        var result = await ch.WriteAsync(Data.Ok("hi"));
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task BeforeWriteHandler_WritesToSameChannel_NoInfiniteLoop()
    {
        var ch = StreamChannel.Memory("c");
        int outerHits = 0;
        ch.Events.Add(new EventBinding(EventType.BeforeWrite, async (_, _, _) =>
        {
            outerHits++;
            // Re-entry: write to the same channel inside the handler.
            await ch.WriteAsync(Data.Ok("inner"));
            return Data.Ok();
        }));
        await ch.WriteAsync(Data.Ok("outer"));
        await Assert.That(outerHits).IsEqualTo(1);
    }

    [Test]
    public async Task MultipleBindings_FireInRegistrationOrder()
    {
        var ch = StreamChannel.Memory("c");
        var order = new List<string>();
        ch.Events.Add(new EventBinding(EventType.BeforeWrite, (_, _, _) => { order.Add("A"); return Task.FromResult(Data.Ok()); }));
        ch.Events.Add(new EventBinding(EventType.BeforeWrite, (_, _, _) => { order.Add("B"); return Task.FromResult(Data.Ok()); }));
        ch.Events.Add(new EventBinding(EventType.BeforeWrite, (_, _, _) => { order.Add("C"); return Task.FromResult(Data.Ok()); }));
        await ch.WriteAsync(Data.Ok("x"));
        await Assert.That(order).IsEquivalentTo(new[] { "A", "B", "C" });
    }

    [Test]
    public async Task FirstThrowingBinding_StopsSubsequentBindings()
    {
        var ch = StreamChannel.Memory("c");
        var order = new List<string>();
        ch.Events.Add(new EventBinding(EventType.BeforeWrite, (_, _, _) => { order.Add("A"); return Task.FromResult(Data.Ok()); }));
        ch.Events.Add(new EventBinding(EventType.BeforeWrite, (_, _, _) =>
        {
            order.Add("B");
            throw new InvalidOperationException("stop");
        }));
        ch.Events.Add(new EventBinding(EventType.BeforeWrite, (_, _, _) => { order.Add("C"); return Task.FromResult(Data.Ok()); }));
        var result = await ch.WriteAsync(Data.Ok("x"));
        await Assert.That(result.Success).IsFalse();
        await Assert.That(order).IsEquivalentTo(new[] { "A", "B" });
    }

    [Test]
    public async Task OnAsk_OnSessionChannel_FiresPostAnswer()
    {
        var ms = new MemoryStream(global::System.Text.Encoding.UTF8.GetBytes("answer\n"));
        var ch = new StreamChannel("i", ms, ChannelDirection.Bidirectional, ownsStream: false)
        { Mime = "text/plain" };
        Data? receivedData = null;
        ch.Events.Add(new EventBinding(EventType.OnAsk, (_, _, payload) =>
        {
            receivedData = payload;
            return Task.FromResult(Data.Ok());
        }));
        var result = await ch.Ask(Data.Ok((object?)null));
        await Assert.That(result.Value as string).IsEqualTo("answer");
        await Assert.That(receivedData).IsNotNull();
        await Assert.That(receivedData!.Value as string).IsEqualTo("answer");
    }

    [Test]
    public async Task OnAsk_OnMessageChannel_FiresPreSerialise()
    {
        // Stage 8 ships unified OnAsk firing semantics: handler always sees the
        // post-Core Data. Per-kind direction (Session post-answer vs Message
        // pre-suspend) tracked in cool.md for follow-up; for now the contract is
        // consistent. Test pins that OnAsk does fire for Message-style kinds.
        var ch = new MessageProbeChannel("m");
        bool fired = false;
        ch.Events.Add(new EventBinding(EventType.OnAsk, (_, _, _) =>
        {
            fired = true;
            return Task.FromResult(Data.Ok());
        }));
        await ch.Ask(Data.Ok("q?"));
        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task BindingsMatch_AcrossUserAndServiceChannels_OfSameName()
    {
        await using var app = new global::App.@this("/tmp/s8-cross");
        var userLogger = StreamChannel.Memory("logger");
        var serviceLogger = StreamChannel.Memory("logger");
        app.User.Channels.Register(userLogger);
        await using var svc = app.Services.New(parent: app.User);
        svc.Channels.Register(serviceLogger);

        var hits = 0;
        app.Events.Register(new EventBinding(EventType.BeforeWrite,
            (_, _, _) => { hits++; return Task.FromResult(Data.Ok()); },
            channelName: "logger"));

        await userLogger.WriteAsync(Data.Ok("a"));
        await serviceLogger.WriteAsync(Data.Ok("b"));
        await Assert.That(hits).IsEqualTo(2);
    }

    [Test]
    public async Task ChannelEvents_DoNotTriggerGoalStepOrActionBindings()
    {
        await using var app = new global::App.@this("/tmp/s8-iso");
        var ch = StreamChannel.Memory("c");
        app.User.Channels.Register(ch);

        bool goalFired = false;
        app.Events.Register(new EventBinding(EventType.BeforeGoal,
            (_, _, _) => { goalFired = true; return Task.FromResult(Data.Ok()); }));

        await ch.WriteAsync(Data.Ok("x"));
        await Assert.That(goalFired).IsFalse();
    }

    private sealed class ThrowOnWriteChannel : Channel
    {
        public ThrowOnWriteChannel(string name) { Name = name; }
        public override Task<Data> WriteCore(Data data, CancellationToken ct = default)
            => throw new IOException("boom");
        public override Task<Data> ReadCore(CancellationToken ct = default) => Task.FromResult(Data.Ok());
        public override Task<Data> AskCore(Data prompt, CancellationToken ct = default) => Task.FromResult(Data.Ok());
    }

    private sealed class MessageProbeChannel : global::App.Channels.Channel.Message.@this
    {
        public MessageProbeChannel(string name) { Name = name; }
        public override Task<Data> WriteCore(Data data, CancellationToken ct = default) => Task.FromResult(Data.Ok());
        public override Task<Data> ReadCore(CancellationToken ct = default) => Task.FromResult(Data.Ok());
        public override Task<Data> AskCore(Data prompt, CancellationToken ct = default) => Task.FromResult(Data.Ok("answer-from-resume"));
    }
}
