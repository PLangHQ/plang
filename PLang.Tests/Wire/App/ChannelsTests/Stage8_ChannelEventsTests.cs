using app.@event;
using app.@event.lifecycle.binding;

namespace PLang.Tests.App.ChannelsTests;

// Channel events: types, firing, recursion guard.
// Architect: stage-8-channel-events.md and v1/plan/channel-events.md.

public class Stage8_ChannelEventsTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create(
        "/tmp/s8-" + System.Guid.NewGuid().ToString("N")[..6]);

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task Trigger_HasFiveNewValues_ForChannelLifecycle()
    {
        var names = Enum.GetNames(typeof(Trigger));
        await Assert.That(names).Contains("BeforeWrite");
        await Assert.That(names).Contains("AfterWrite");
        await Assert.That(names).Contains("BeforeRead");
        await Assert.That(names).Contains("AfterRead");
        await Assert.That(names).Contains("OnAsk");
    }

    [Test]
    public async Task EventBinding_AcceptsChannelNameFilter()
    {
        var b = new EventBinding(Trigger.BeforeWrite,
            (_, _, _) => Task.FromResult(app.Ok()),
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
        await using var app = global::PLang.Tests.TestApp.Create("/test", autoWireConsoleChannels: false);
        var ch = StreamChannel.Memory("logger");
        app.User.Channel.Register(ch);
        Data? captured = null;
        ch.Events.Add(new EventBinding(Trigger.BeforeWrite, (_, _, payload) =>
        {
            captured = payload;
            return Task.FromResult(app.Ok());
        }, channelName: "logger"));

        await ch.WriteAsync(app.Ok("hello"));
        await Assert.That(captured).IsNotNull();
        await Assert.That((await captured!.Value())?.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task BeforeWriteHandler_ThrowingAborts_AfterWriteDoesNotFire()
    {
        var ch = StreamChannel.Memory("c");
        bool afterFired = false;
        ch.Events.Add(new EventBinding(Trigger.BeforeWrite, (_, _, _) =>
            throw new InvalidOperationException("nope")));
        ch.Events.Add(new EventBinding(Trigger.AfterWrite, (_, _, _) =>
        {
            afterFired = true;
            return Task.FromResult(app.Ok());
        }));

        var result = await ch.WriteAsync(app.Ok("hi"));
        await result.IsFailure();
        await Assert.That(afterFired).IsFalse();
    }

    [Test]
    public async Task AfterWriteHandler_FiresWhenWriteCoreSucceeds()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test", autoWireConsoleChannels: false);
        var ch = StreamChannel.Memory("c");
        app.User.Channel.Register(ch);
        bool afterFired = false;
        ch.Events.Add(new EventBinding(Trigger.AfterWrite, (_, _, _) =>
        {
            afterFired = true;
            return Task.FromResult(app.Ok());
        }));
        var result = await ch.WriteAsync(app.Ok("hi"));
        await result.IsSuccess();
        await Assert.That(afterFired).IsTrue();
    }

    [Test]
    public async Task AfterWriteHandler_FiresWhenWriteCoreThrows()
    {
        var ch = new ThrowOnWriteChannel("c");
        bool afterFired = false;
        Data? receivedData = null;
        ch.Events.Add(new EventBinding(Trigger.AfterWrite, (_, _, payload) =>
        {
            afterFired = true;
            receivedData = payload;
            return Task.FromResult(app.Ok());
        }));
        var result = await ch.WriteAsync(app.Ok("hi"));
        await result.IsFailure();
        await Assert.That(afterFired).IsTrue();
        await receivedData!.IsFailure();
    }

    [Test]
    public async Task AfterWriteHandler_ThrowingIsSuppressed_OriginalOutcomeStands()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test", autoWireConsoleChannels: false);
        var ch = StreamChannel.Memory("c");
        app.User.Channel.Register(ch);
        ch.Events.Add(new EventBinding(Trigger.AfterWrite, (_, _, _) =>
            throw new InvalidOperationException("after fail")));
        var result = await ch.WriteAsync(app.Ok("hi"));
        await result.IsSuccess();
    }

    [Test]
    public async Task BeforeWriteHandler_WritesToSameChannel_NoInfiniteLoop()
    {
        var ch = StreamChannel.Memory("c");
        int outerHits = 0;
        ch.Events.Add(new EventBinding(Trigger.BeforeWrite, async (_, _, _) =>
        {
            outerHits++;
            // Re-entry: write to the same channel inside the handler.
            await ch.WriteAsync(app.Ok("inner"));
            return app.Ok();
        }));
        await ch.WriteAsync(app.Ok("outer"));
        await Assert.That(outerHits).IsEqualTo(1);
    }

    [Test]
    public async Task MultipleBindings_FireInRegistrationOrder()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test", autoWireConsoleChannels: false);
        var ch = StreamChannel.Memory("c");
        app.User.Channel.Register(ch);
        var order = new List<string>();
        ch.Events.Add(new EventBinding(Trigger.BeforeWrite, (_, _, _) => { order.Add("A"); return Task.FromResult(app.Ok()); }));
        ch.Events.Add(new EventBinding(Trigger.BeforeWrite, (_, _, _) => { order.Add("B"); return Task.FromResult(app.Ok()); }));
        ch.Events.Add(new EventBinding(Trigger.BeforeWrite, (_, _, _) => { order.Add("C"); return Task.FromResult(app.Ok()); }));
        await ch.WriteAsync(app.Ok("x"));
        await Assert.That(order).IsEquivalentTo(new[] { "A", "B", "C" });
    }

    [Test]
    public async Task FirstThrowingBinding_StopsSubsequentBindings()
    {
        var ch = StreamChannel.Memory("c");
        var order = new List<string>();
        ch.Events.Add(new EventBinding(Trigger.BeforeWrite, (_, _, _) => { order.Add("A"); return Task.FromResult(app.Ok()); }));
        ch.Events.Add(new EventBinding(Trigger.BeforeWrite, (_, _, _) =>
        {
            order.Add("B");
            throw new InvalidOperationException("stop");
        }));
        ch.Events.Add(new EventBinding(Trigger.BeforeWrite, (_, _, _) => { order.Add("C"); return Task.FromResult(app.Ok()); }));
        var result = await ch.WriteAsync(app.Ok("x"));
        await result.IsFailure();
        await Assert.That(order).IsEquivalentTo(new[] { "A", "B" });
    }

    [Test]
    public async Task OnAsk_OnSessionChannel_FiresPostAnswer()
    {
        var ms = new MemoryStream(global::System.Text.Encoding.UTF8.GetBytes("answer\n"));
        var ch = new StreamChannel("i", ms, ChannelDirection.Bidirectional, ownsStream: false)
        { Mime = "text/plain" };
        Data? receivedData = null;
        ch.Events.Add(new EventBinding(Trigger.OnAsk, (_, _, payload) =>
        {
            receivedData = payload;
            return Task.FromResult(app.Ok());
        }));
        var result = await ch.AskAsync(new global::app.module.output.ask(app.User.Context) { Question = new global::app.data.@this<global::app.type.item.text.@this>("", "") });
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("answer");
        await Assert.That(receivedData).IsNotNull();
        await Assert.That((await receivedData!.Value())?.ToString()).IsEqualTo("answer");
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
        ch.Events.Add(new EventBinding(Trigger.OnAsk, (_, _, _) =>
        {
            fired = true;
            return Task.FromResult(app.Ok());
        }));
        await ch.AskAsync(new global::app.module.output.ask(app.User.Context) { Question = new global::app.data.@this<global::app.type.item.text.@this>("", "q?") });
        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task BindingsMatch_AcrossUserAndServiceChannels_OfSameName()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/tmp/s8-cross");
        var userLogger = StreamChannel.Memory("logger");
        var serviceLogger = StreamChannel.Memory("logger");
        app.User.Channel.Register(userLogger);
        await using var svc = app.Services.New(parent: app.User);
        svc.Channels.Register(serviceLogger);

        var hits = 0;
        app.Event.Register(new EventBinding(Trigger.BeforeWrite,
            (_, _, _) => { hits++; return Task.FromResult(app.Ok()); },
            channelName: "logger"));

        await userLogger.WriteAsync(app.Ok("a"));
        await serviceLogger.WriteAsync(app.Ok("b"));
        await Assert.That(hits).IsEqualTo(2);
    }

    [Test]
    public async Task ChannelEvents_DoNotTriggerGoalStepOrActionBindings()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/tmp/s8-iso");
        var ch = StreamChannel.Memory("c");
        app.User.Channel.Register(ch);

        bool goalFired = false;
        app.Event.Register(new EventBinding(Trigger.BeforeGoal,
            (_, _, _) => { goalFired = true; return Task.FromResult(app.Ok()); }));

        await ch.WriteAsync(app.Ok("x"));
        await Assert.That(goalFired).IsFalse();
    }

    [Test]
    public async Task EventsActiveSet_IsInstanceScoped_NotShared()
    {
        // Regression probe for B1: `_active` is an instance field, not static.
        // If it ever becomes static, evB sees evA's active set.
        var evA = new global::app.channel.@event.@this();
        var evB = new global::app.channel.@event.@this();
        using var _ = evA.Enter("X");
        await Assert.That(evA.IsActive("X")).IsTrue();
        await Assert.That(evB.IsActive("X")).IsFalse();
    }

    [Test]
    public async Task Enter_FromConcurrentChild_DoesNotLeakChildIdToParentFlow()
    {
        // Regression probe for L1: Enter must copy-on-write.
        // If a child mutates the parent's HashSet in place, the parent flow
        // sees the child's id while the child is still inside its scope.
        // (Naive Task.WhenAll passes either way — children Add then Remove.)
        var ev = new global::app.channel.@event.@this();
        using var _ = ev.Enter("A");
        var inside = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var t = Task.Run(async () =>
        {
            using var __ = ev.Enter("B");
            inside.SetResult();
            await release.Task;
        });
        await inside.Task;
        var leaked = ev.IsActive("B");
        release.SetResult();
        await t;
        await Assert.That(leaked).IsFalse();
    }

    private sealed class ThrowOnWriteChannel : Channel
    {
        public ThrowOnWriteChannel(string name) { Name = name; }
        public override Task<Data> Write(Data data, CancellationToken ct = default)
            => throw new IOException("boom");
        public override Task<Data> Read(CancellationToken ct = default) => Task.FromResult(Data.Ok());
        public override Task<Data> Ask(global::app.module.output.ask action, CancellationToken ct = default) => Task.FromResult(Data.Ok());
    }

    private sealed class MessageProbeChannel : global::app.channel.type.message.@this
    {
        public MessageProbeChannel(string name) { Name = name; }
        public override Task<Data> Write(Data data, CancellationToken ct = default) => Task.FromResult(Data.Ok());
        public override Task<Data> Read(CancellationToken ct = default) => Task.FromResult(Data.Ok());
        public override Task<Data> Ask(global::app.module.output.ask action, CancellationToken ct = default) => Task.FromResult(Data.Ok("answer-from-resume"));
    }
}
