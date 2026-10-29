using app.@event;
using When = global::app.@event.When;

namespace PLang.Tests.App.ChannelsTests;

// A channel fires its own events: write, read and ask through on.write / on.read / on.ask, at the channel type's
// level and its own. Before is handed what is about to happen — a failure or a Handled answer is the operation's
// answer and the channel's own work doesn't run; every after runs on the result, and a failing after is the result.
// The channel's own transport failure is an error result; a binding's throw follows the binding list's rule. A
// binding doesn't fire inside itself within one flow; parallel flows each fire it.
public class Stage8_ChannelEventsTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this(
        "/tmp/s8-" + System.Guid.NewGuid().ToString("N")[..6], global::PLang.Tests.Parent.Actor).Testing();

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.channel.@this Registered(string name)
    {
        var ch = StreamChannel.Memory(app.actor.list.User.Channel, name);
        app.actor.list.User.Channel.Register(ch);
        return ch;
    }

    private async Task Bind(global::app.type.item.@this on, string @event, When when,
        System.Func<global::app.type.item.@this, Data, global::app.actor.context.@this, Task<Data>> handler)
        => await app.actor.list.User.@event.Add(new global::app.@event.code.@this(on, @event, when, handler, app.actor.list.User, global::app.@event.code.@this.Always));

    [Test]
    public async Task AChannel_IsAnItem_WithItsOwnEvents()
    {
        var ch = StreamChannel.Memory(app.actor.list.User.Channel, "c");
        await Assert.That((await app.actor.list.User.@event.On(ch)).write.before.Count).IsEqualTo(0);
        await Assert.That(ReferenceEquals(ch.Clone(), ch)).IsTrue();
        await Assert.That(app.type.list["channel"].Namespace).IsEqualTo("app.channel");
        // a stream channel is a kind of channel: {channel, stream}
        await Assert.That(ch.Type.Name).IsEqualTo("channel");
        await Assert.That(ch.Type.kind.Name).IsEqualTo("stream");
    }

    [Test]
    public async Task BeforeWrite_IsHandedTheData()
    {
        var ch = Registered("logger");
        Data? captured = null;
        await Bind(ch, "write", When.before, (_, data, ctx) => { captured = data; return Task.FromResult(ctx.Ok()); });

        await ch.WriteAsync(app.Ok("hello"));

        await Assert.That((await captured!.Value())?.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task ARefusingBeforeWrite_IsTheAnswer_TheAftersStillRunOnIt()
    {
        var ch = Registered("c");
        Data? after = null;
        await Bind(ch, "write", When.before, (_, _, ctx) => Task.FromResult(ctx.Error(new global::app.error.@this("no", "Refused", 400))));
        await Bind(ch, "write", When.after, (_, result, ctx) => { after = result; return Task.FromResult(ctx.Ok()); });

        var result = await ch.WriteAsync(app.Ok("hi"));

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Refused");
        await Assert.That(after!.Error?.Key).IsEqualTo("Refused");
    }

    [Test]
    public async Task ABeforeWriteThatThrows_Propagates_NoChannelCatch()
    {
        var ch = Registered("c");
        await Bind(ch, "write", When.before, (_, _, _) => throw new InvalidOperationException("nope"));

        await Assert.That(async () => await ch.WriteAsync(app.Ok("hi"))).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task AfterWrite_FiresWhenTheWriteSucceeds()
    {
        var ch = Registered("c");
        var fired = false;
        await Bind(ch, "write", When.after, (_, _, ctx) => { fired = true; return Task.FromResult(ctx.Ok()); });

        var result = await ch.WriteAsync(app.Ok("hi"));

        await result.IsSuccess();
        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task ATransportFailure_IsAnErrorResult_AndAfterWriteSeesIt()
    {
        var ch = new ThrowOnWriteChannel(app.actor.list.User.Channel, "c", app.actor.list.User.Context);
        app.actor.list.User.Channel.Register(ch);
        Data? received = null;
        await Bind(ch, "write", When.after, (_, result, ctx) => { received = result; return Task.FromResult(ctx.Ok()); });

        var result = await ch.WriteAsync(app.Ok("hi"));

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("WriteError");
        await received!.IsFailure();
    }

    [Test]
    public async Task AFailingAfterWrite_IsTheResult()
    {
        var ch = Registered("c");
        await Bind(ch, "write", When.after, (_, _, ctx) => Task.FromResult(ctx.Error(new global::app.error.@this("after", "Broke", 400))));

        var result = await ch.WriteAsync(app.Ok("hi"));

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Broke");
    }

    [Test]
    public async Task ABeforeWriteThatWritesToItsOwnChannel_FiresOnce()
    {
        var ch = Registered("c");
        var hits = 0;
        await Bind(ch, "write", When.before, async (_, _, ctx) =>
        {
            hits++;
            await ch.WriteAsync(app.Ok("inner"));
            return ctx.Ok();
        });

        await ch.WriteAsync(app.Ok("outer"));

        await Assert.That(hits).IsEqualTo(1);
    }

    [Test]
    public async Task ParallelFlowsOnOneContext_EachFireTheBinding()
    {
        var ch = Registered("c");
        var hits = 0;
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        await Bind(ch, "write", When.before, async (_, _, ctx) =>
        {
            if (Interlocked.Increment(ref hits) == 2) entered.TrySetResult();
            await Task.WhenAny(release.Task, Task.Delay(2000));
            return ctx.Ok();
        });

        var first = Task.Run(() => ch.WriteAsync(app.Ok("a")));
        var second = Task.Run(() => ch.WriteAsync(app.Ok("b")));
        await Task.WhenAny(entered.Task, Task.Delay(2000));
        release.TrySetResult();
        await Task.WhenAll(first, second);

        await Assert.That(hits).IsEqualTo(2);
    }

    [Test]
    public async Task Bindings_FireInTheOrderAdded()
    {
        var ch = Registered("c");
        var order = new List<string>();
        await Bind(ch, "write", When.before, (_, _, ctx) => { order.Add("A"); return Task.FromResult(ctx.Ok()); });
        await Bind(ch, "write", When.before, (_, _, ctx) => { order.Add("B"); return Task.FromResult(ctx.Ok()); });
        await Bind(ch, "write", When.before, (_, _, ctx) => { order.Add("C"); return Task.FromResult(ctx.Ok()); });

        await ch.WriteAsync(app.Ok("x"));

        await Assert.That(order).IsEquivalentTo(new[] { "A", "B", "C" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task AfterAsk_OnASessionChannel_IsHandedTheAnswer()
    {
        var ms = new MemoryStream(global::System.Text.Encoding.UTF8.GetBytes("answer\n"));
        var ch = new StreamChannel(app.actor.list.User.Channel, "i", ms, ChannelDirection.Bidirectional, ownsStream: false) { Mime = "text/plain" };
        app.actor.list.User.Channel.Register(ch);
        Data? received = null;
        await Bind(ch, "ask", When.after, (_, result, ctx) => { received = result; return Task.FromResult(ctx.Ok()); });

        var result = await ch.AskAsync(new global::app.module.output.ask(app.actor.list.User.Context) { Question = new global::app.data.@this<global::app.type.item.text.@this>("", "", context: app.actor.list.User.Context) });

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("answer");
        await Assert.That((await received!.Value())?.ToString()).IsEqualTo("answer");
    }

    // A question nobody answers before its asker stops waiting: the ask answers Cancelled, not a transport failure.
    [Test]
    public async Task AnAskCancelledWhileWaiting_AnswersCancelled()
    {
        var pipe = new global::System.IO.Pipelines.Pipe();
        var ch = new StreamChannel(app.actor.list.User.Channel, "i3", pipe.Reader.AsStream(), ChannelDirection.Input, ownsStream: false) { Mime = "text/plain" };
        app.actor.list.User.Channel.Register(ch);
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var result = await ch.AskAsync(new global::app.module.output.ask(app.actor.list.User.Context)
            { Question = new global::app.data.@this<global::app.type.item.text.@this>("", "", context: app.actor.list.User.Context) }, stop.Token);

        await Assert.That(result.Error?.Key).IsEqualTo("Cancelled");
    }

    // Two answers already in the stream (piped in at once): each question gets its own — the reader that read past
    // the first answer is the one the second question reads from.
    [Test]
    public async Task TwoAsks_TwoAnswersInTheStream_EachGetsItsOwn()
    {
        var ms = new MemoryStream(global::System.Text.Encoding.UTF8.GetBytes("first\nsecond\n"));
        var ch = new StreamChannel(app.actor.list.User.Channel, "i2", ms, ChannelDirection.Bidirectional, ownsStream: false) { Mime = "text/plain" };
        app.actor.list.User.Channel.Register(ch);
        global::app.module.output.ask Ask() => new(app.actor.list.User.Context) { Question = new global::app.data.@this<global::app.type.item.text.@this>("", "", context: app.actor.list.User.Context) };

        var first = await ch.AskAsync(Ask());
        var second = await ch.AskAsync(Ask());

        await Assert.That((await first.Value())?.ToString()).IsEqualTo("first");
        await Assert.That((await second.Value())?.ToString()).IsEqualTo("second");
    }

    [Test]
    public async Task AfterAsk_OnAMessageChannel_Fires()
    {
        var ch = new MessageProbeChannel(app.actor.list.User.Channel, "m", app.actor.list.User.Context);
        app.actor.list.User.Channel.Register(ch);
        var fired = false;
        await Bind(ch, "ask", When.after, (_, _, ctx) => { fired = true; return Task.FromResult(ctx.Ok()); });

        await ch.AskAsync(new global::app.module.output.ask(app.actor.list.User.Context) { Question = new global::app.data.@this<global::app.type.item.text.@this>("", "q?", context: app.actor.list.User.Context) });

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task TheChannelTypesWrite_RunsForTheBindingActorsChannels_NotAServicesOwn()
    {
        await using var app = new global::app.@this("/tmp/s8-cross", global::PLang.Tests.Parent.Actor).Testing();
        var userLogger = StreamChannel.Memory(app.actor.list.User.Channel, "logger");
        app.actor.list.User.Channel.Register(userLogger);
        await using var svc = app.Services.New(parent: app.actor.list.User);
        var serviceLogger = StreamChannel.Memory(svc.Channels, "logger");
        svc.Channels.Register(serviceLogger);
        var hits = 0;
        await app.actor.list.User.@event.Add(new global::app.@event.code.@this(app.type.list["channel"], "write", global::app.@event.When.before, (_, _, ctx) => { Interlocked.Increment(ref hits); return Task.FromResult(ctx.Ok()); }, app.actor.list.User, global::app.@event.code.@this.Always));

        await userLogger.WriteAsync(app.Ok("a"));
        await serviceLogger.WriteAsync(app.Ok("b"));

        // the user's event runs for the user's channel; the service is its own actor, with its own events
        await Assert.That(hits).IsEqualTo(1);
    }

    [Test]
    public async Task AChannelWrite_FiresNoGoalStepOrActionBinding()
    {
        var ch = Registered("c");
        var fired = false;
        await app.actor.list.User.@event.Add(new global::app.@event.code.@this(app.goal.type, "start", global::app.@event.When.before, (_, _, ctx) => { fired = true; return Task.FromResult(ctx.Ok()); }, app.actor.list.User, global::app.@event.code.@this.Always));

        await ch.WriteAsync(app.Ok("x"));

        await Assert.That(fired).IsFalse();
    }

    private sealed class ThrowOnWriteChannel : Channel
    {
        private readonly global::app.actor.context.@this _ctx;
        public ThrowOnWriteChannel(global::app.channel.list.@this channels, string name, global::app.actor.context.@this ctx) : base(channels) { Name = name; _ctx = ctx; }
        public override Task<Data> Write(Data data, CancellationToken ct = default)
            => throw new IOException("boom");
        public override Task<Data> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<Data> Ask(global::app.module.output.ask action, CancellationToken ct = default) => Task.FromResult(action.Context.Ok());
    }

    private sealed class MessageProbeChannel : global::app.channel.type.message.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public MessageProbeChannel(global::app.channel.list.@this channels, string name, global::app.actor.context.@this ctx) : base(channels) { Name = name; _ctx = ctx; }
        public override Task<Data> Write(Data data, CancellationToken ct = default) => Task.FromResult(data.Context.Ok());
        public override Task<Data> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<Data> Ask(global::app.module.output.ask action, CancellationToken ct = default) => Task.FromResult(action.Context.Ok("answer-from-resume"));
    }
}
