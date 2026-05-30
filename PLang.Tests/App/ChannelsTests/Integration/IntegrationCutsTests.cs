using app.@event;
using app.@event.lifecycle.binding;

namespace PLang.Tests.App.ChannelsTests.Integration;

// End-to-end integration cuts. md.

public class IntegrationCutsTests
{
    [Test]
    public async Task Cut1_ConsoleBoot_ThroughWriteOut_ReachesStdout()
    {
        await using var app = new global::app.@this("/tmp/cut1");
        var userOutput = new MemoryStream();
        var userError = new MemoryStream();
        var userInput = new MemoryStream();
        app.User.Channel.Register(new StreamChannel("output", userOutput, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });
        app.User.Channel.Register(new StreamChannel("error", userError, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });
        app.User.Channel.Register(new StreamChannel("input", userInput, ChannelDirection.Input, ownsStream: false)
        { Mime = "text/plain" });
        global::app.@this.WireDefaultConsoleChannels(app.System);

        // Direct write through the resolved Output channel — proves
        // Channels.Resolve(null) returns Output role channel and WriteAsync routes there.
        var ch = app.User.Channel.Resolve(null);
        await ch.WriteAsync(Data.Ok("hello"));

        var got = global::System.Text.Encoding.UTF8.GetString(userOutput.ToArray());
        await Assert.That(got.Contains("hello")).IsTrue();
        await Assert.That(userError.Length).IsEqualTo(0L);
    }

    // Cut2 removed: tested the FreezeFoundational / foundational-snapshot
    // recursion guard. Replaced by per-channel IsExecuting flag in
    // commit 827d34e19 (channels: replace foundational snapshot with
    // per-channel IsExecuting recursion guard). Stage3
    // Channels_Get_TreatsExecutingGoalChannelAsNotFound is the invariant
    // test now.

    [Test]
    public async Task Cut3_ChannelEvents_AbortPlusAuditMetric_AcrossTwoWrites()
    {
        await using var app = new global::app.@this("/tmp/cut3");
        var auditCapture = new MemoryStream();
        var metricsCapture = new MemoryStream();
        var audit = new StreamChannel("audit.external", auditCapture, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" };
        var metrics = new StreamChannel("metrics", metricsCapture, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" };
        app.User.Channel.Register(audit);
        app.User.Channel.Register(metrics);

        // BeforeWrite on audit: reject if value contains "REJECT".
        audit.Events.Add(new EventBinding(EventType.BeforeWrite, (_, _, payload) =>
        {
            if (payload?.Value is string s && s.Contains("REJECT"))
                throw new InvalidOperationException("rejected by approval");
            return Task.FromResult(Data.Ok());
        }));

        // AfterWrite on audit: write "+1" to metrics. Stage 8 contract:
        // BeforeWrite-abort suppresses AfterWrite — so metrics fires only on
        // the successful write.
        audit.Events.Add(new EventBinding(EventType.AfterWrite, async (_, _, _) =>
        {
            await metrics.WriteAsync(Data.Ok("+1"));
            return Data.Ok();
        }));

        var ok = await audit.WriteAsync(Data.Ok("ok-payload"));
        var bad = await audit.WriteAsync(Data.Ok("REJECT-this"));

        await ok.IsSuccess();
        await bad.IsFailure();

        var auditText = global::System.Text.Encoding.UTF8.GetString(auditCapture.ToArray());
        await Assert.That(auditText.Contains("ok-payload")).IsTrue();
        await Assert.That(auditText.Contains("REJECT")).IsFalse();

        var metricsText = global::System.Text.Encoding.UTF8.GetString(metricsCapture.ToArray());
        await Assert.That(metricsText.Contains("+1")).IsTrue();
    }

    private sealed class GoalChannelProbe : global::app.channel.goal.@this
    {
        private readonly Action _onInvoke;
        public GoalChannelProbe(string name, global::app.goal.@this goal, global::app.actor.@this actor, Action onInvoke)
            : base(name, goal, actor) { _onInvoke = onInvoke; }
        public override Task<Data> Write(Data data, CancellationToken ct = default)
        {
            _onInvoke();
            return Task.FromResult(Data.Ok());
        }
    }
}
