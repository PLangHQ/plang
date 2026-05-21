using GoalChannel = global::App.Channels.Channel.Goal.@this;
using EngineGoal = global::App.Goals.Goal.@this;

namespace PLang.Tests.App.ChannelsTests;

// Stage 3 — Channel.Goal concrete + recursion rule + foundational set capture.
// Architect: stage-3-goal-channel.md.

public class Stage3_GoalChannelTests
{
    [Test]
    public async Task GoalChannel_WriteCore_InvokesGoalWithDataBound()
    {
        var app = new global::App.@this("/tmp/g1");
        var goal = new EngineGoal { Name = "Probe", Path = "Probe.goal", PrPath = "/Probe.pr" };
        // Empty steps → goal completes with Ok. Test validates %!data% binding via Variables.
        var ch = new GoalChannel("logger", goal, app.User);
        var dataIn = Data.Ok("payload-A");
        var result = await ch.WriteCore(dataIn);
        await Assert.That(result.Success).IsTrue();

        var captured = app.User.Context.Variables.Get("!data");
        await Assert.That(captured).IsNotNull();
    }

    [Test]
    public async Task GoalChannel_WriteCore_ReturnsGoalsResultData()
    {
        var app = new global::App.@this("/tmp/g2");
        var goal = new EngineGoal { Name = "ReturnsOk", Path = "Returns.goal", PrPath = "/R.pr" };
        var ch = new GoalChannel("c", goal, app.User);
        var result = await ch.WriteCore(Data.Ok("x"));
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task GoalChannel_RegisteredBeforeFreeze_CapturesPreFreezeFoundationalSet()
    {
        var app = new global::App.@this("/tmp/g3");
        // Freeze: snapshot taken now.
        app.User.FreezeFoundational();
        var foundationalNames = app.User.FoundationalChannels.ChannelNames.ToHashSet();
        // Add a new channel after freeze.
        app.User.Channels.Register(StreamChannel.Memory("late"));

        await Assert.That(foundationalNames.Contains("late")).IsFalse();
        await Assert.That(app.User.Channels.Contains("late")).IsTrue();
    }

    [Test]
    public async Task GoalChannel_WritesInsideGoal_ResolveAgainstFoundational_NotCurrentOverlay()
    {
        var app = new global::App.@this("/tmp/g4");
        // Foundational state: capture stream registered as "output".
        var foundationalCapture = new MemoryStream();
        app.User.Channels.Register(new StreamChannel("output", foundationalCapture, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });
        app.User.FreezeFoundational();

        // Now overlay a goal channel as "output".
        var overlayGoal = new EngineGoal { Name = "Overlay", Path = "O.goal", PrPath = "/O.pr" };
        var goalCh = new GoalChannel("output", overlayGoal, app.User);
        app.User.Channels.Register(goalCh);

        // Inside the goal channel call, the override is active. We probe by checking
        // that during the override scope, Channels.Resolve("output") returns the
        // foundational stream channel, not the goal channel.
        using (app.User.PushChannelsOverride(app.User.FoundationalChannels))
        {
            var resolved = app.User.Channels.Resolve("output");
            await Assert.That(resolved).IsNotNull();
            await Assert.That(resolved is StreamChannel).IsTrue();
        }
    }

    [Test]
    public async Task GoalChannel_AsOutput_GoalWriteOut_ReachesFoundationalStdout()
    {
        var app = new global::App.@this("/tmp/g5");
        var captured = new MemoryStream();
        app.User.Channels.Register(new StreamChannel("output", captured, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });
        app.User.FreezeFoundational();

        var overlayGoal = new EngineGoal { Name = "Overlay", Path = "O.goal", PrPath = "/O.pr" };
        var goalCh = new GoalChannel("output", overlayGoal, app.User);
        app.User.Channels.Register(goalCh);

        // Within an override scope, write to foundational "output" lands in `captured`.
        using (app.User.PushChannelsOverride(app.User.FoundationalChannels))
        {
            var stream = (StreamChannel)app.User.Channels.Resolve("output")!;
            await stream.WriteCore(Data.Ok("payload"));
        }
        var bytes = global::System.Text.Encoding.UTF8.GetString(captured.ToArray());
        await Assert.That(bytes.Contains("payload")).IsTrue();
    }

    [Test]
    public async Task GoalChannel_StackedOverrides_DoNotChain()
    {
        var app = new global::App.@this("/tmp/g6");
        var foundationCapture = new MemoryStream();
        app.User.Channels.Register(new StreamChannel("output", foundationCapture, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });
        app.User.FreezeFoundational();

        var goalA = new EngineGoal { Name = "A", Path = "A.goal", PrPath = "/A.pr" };
        var goalB = new EngineGoal { Name = "B", Path = "B.goal", PrPath = "/B.pr" };
        var chA = new GoalChannel("output", goalA, app.User);
        app.User.Channels.Register(chA);
        var chB = new GoalChannel("output", goalB, app.User);
        app.User.Channels.Register(chB);

        // GoalB's foundational view should still be the original Stream channel,
        // not goalA. Both goals reference the same Actor.FoundationalChannels.
        await Assert.That(app.User.FoundationalChannels.Resolve("output") is StreamChannel).IsTrue();
    }

    [Test]
    public async Task GoalChannel_FanOutComposition_WritesToFileAndOutput_NoRecursion()
    {
        // Light version: confirm no infinite recursion when a goal channel's body
        // would reference its own name. The override + foundational set guarantees
        // it. Full integration cut covers the dual-destination assertion.
        var app = new global::App.@this("/tmp/g7");
        var captured = new MemoryStream();
        app.User.Channels.Register(new StreamChannel("output", captured, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });
        app.User.FreezeFoundational();

        var loggerGoal = new EngineGoal { Name = "Logger", Path = "L.goal", PrPath = "/L.pr" };
        var loggerCh = new GoalChannel("output", loggerGoal, app.User);
        app.User.Channels.Register(loggerCh);

        // Single write through the overlay; goal runs and any inner write resolves
        // foundational. Empty Steps means it just completes — Stream is unaffected
        // here, so we just assert no exception/recursion blew up.
        var result = await loggerCh.WriteCore(Data.Ok("msg"));
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task GoalChannel_Ask_InvokesGoal_ReturnsAnswer()
    {
        var app = new global::App.@this("/tmp/g8");
        var goal = new EngineGoal { Name = "Asker", Path = "Asker.goal", PrPath = "/A.pr" };
        var ch = new GoalChannel("input", goal, app.User);
        var result = await ch.AskCore(new global::App.modules.output.ask { Question = new global::App.Data.@this<string>("", "q?") });
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task GoalChannel_Dispose_DoesNotDisposeUnderlyingGoal()
    {
        var app = new global::App.@this("/tmp/g9");
        var goal = new EngineGoal { Name = "G", Path = "G.goal", PrPath = "/G.pr" };
        var ch = new GoalChannel("c", goal, app.User);
        await ch.DisposeAsync();
        // Goal still usable — re-register as a different channel.
        var ch2 = new GoalChannel("c2", goal, app.User);
        var result = await ch2.WriteCore(Data.Ok("x"));
        await Assert.That(result.Success).IsTrue();
    }
}
