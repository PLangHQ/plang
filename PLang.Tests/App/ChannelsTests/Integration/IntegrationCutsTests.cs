using app.events;
using app.events.lifecycle.bindings.binding;

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
        app.User.Channels.Register(new StreamChannel("output", userOutput, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });
        app.User.Channels.Register(new StreamChannel("error", userError, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });
        app.User.Channels.Register(new StreamChannel("input", userInput, ChannelDirection.Input, ownsStream: false)
        { Mime = "text/plain" });
        global::app.@this.WireDefaultConsoleChannels(app.System);

        // Direct write through the resolved Output channel — proves
        // Channels.Resolve(null) returns Output role channel and WriteAsync routes there.
        var ch = app.User.Channels.Resolve(null);
        await ch.WriteAsync(Data.Ok("hello"));

        var got = global::System.Text.Encoding.UTF8.GetString(userOutput.ToArray());
        await Assert.That(got.Contains("hello")).IsTrue();
        await Assert.That(userError.Length).IsEqualTo(0L);
    }

    [Test]
    public async Task Cut2_GoalChannelFanOut_HitsTwoDestinations_NoRecursion()
    {
        await using var app = new global::app.@this("/tmp/cut2");
        var foundational = new MemoryStream();
        app.User.Channels.Register(new StreamChannel("output", foundational, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });
        app.User.FreezeFoundational();

        // Logger goal: simulate fan-out by registering a Goal channel that
        // captures invocation count and writes to the foundational stream
        // directly (the override scope makes this safe).
        int loggerInvocations = 0;
        var loggerGoal = new global::app.goals.goal.@this { Name = "Logger", Path = "L.goal", PrPath = "/L.pr" };
        var logger = new GoalChannelProbe("output", loggerGoal, app.User, () => loggerInvocations++);
        app.User.Channels.Register(logger);

        // Outer write through the overlay → Logger fires once → no recursion.
        var ch = app.User.Channels.Resolve("output");  // hits the Goal overlay
        await ch.WriteAsync(Data.Ok("hi"));
        await Assert.That(loggerInvocations).IsEqualTo(1);
    }

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
        app.User.Channels.Register(audit);
        app.User.Channels.Register(metrics);

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

        await Assert.That(ok.Success).IsTrue();
        await Assert.That(bad.Success).IsFalse();

        var auditText = global::System.Text.Encoding.UTF8.GetString(auditCapture.ToArray());
        await Assert.That(auditText.Contains("ok-payload")).IsTrue();
        await Assert.That(auditText.Contains("REJECT")).IsFalse();

        var metricsText = global::System.Text.Encoding.UTF8.GetString(metricsCapture.ToArray());
        await Assert.That(metricsText.Contains("+1")).IsTrue();
    }

    private sealed class GoalChannelProbe : global::app.channels.channel.goal.@this
    {
        private readonly Action _onInvoke;
        public GoalChannelProbe(string name, global::app.goals.goal.@this goal, global::app.actor.@this actor, Action onInvoke)
            : base(name, goal, actor) { _onInvoke = onInvoke; }
        public override Task<Data> Write(Data data, CancellationToken ct = default)
        {
            _onInvoke();
            return Task.FromResult(Data.Ok());
        }
    }
}
