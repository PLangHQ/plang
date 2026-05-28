using System.Reflection;
using GoalChannel = global::app.channels.channel.goal.@this;
using EngineGoal = global::app.goals.goal.@this;

namespace PLang.Tests.App.ChannelsTests;

// Channel.Goal concrete + recursion rule.
//
// Recursion isolation lives on GoalChannel.IsExecuting (AsyncLocal):
// while a goal-channel's body is running on the current async context,
// Channels.Get returns null for that name, so a body that writes to its
// own name surfaces ChannelNotFound instead of looping.

public class Stage3_GoalChannelTests
{
    [Test]
    public async Task GoalChannel_WriteCore_InvokesGoalWithDataBound()
    {
        var app = new global::app.@this("/tmp/g1");
        var goal = new EngineGoal { Name = "Probe", Path = "Probe.goal", PrPath = "/Probe.pr" };
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
        var app = new global::app.@this("/tmp/g2");
        var goal = new EngineGoal { Name = "ReturnsOk", Path = "Returns.goal", PrPath = "/R.pr" };
        var ch = new GoalChannel("c", goal, app.User);
        var result = await ch.WriteCore(Data.Ok("x"));
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task GoalChannel_IsExecuting_IsFalseBeforeAndAfterWrite()
    {
        var app = new global::app.@this("/tmp/g_exec");
        var goal = new EngineGoal { Name = "G", Path = "G.goal", PrPath = "/G.pr" };
        var ch = new GoalChannel("x", goal, app.User);
        await Assert.That(ch.IsExecuting).IsFalse();
        await ch.WriteCore(Data.Ok("x"));
        await Assert.That(ch.IsExecuting).IsFalse();
    }

    [Test]
    public async Task Channels_Get_TreatsExecutingGoalChannelAsNotFound()
    {
        // The load-bearing recursion guard: while a goal-channel's body is
        // running, the registry treats that name as not-found, so a body that
        // writes to its own name can't loop back into itself.
        var app = new global::app.@this("/tmp/g_recurse");
        var goal = new EngineGoal { Name = "G", Path = "G.goal", PrPath = "/G.pr" };
        var ch = new GoalChannel("logger", goal, app.User);
        app.User.Channels.Register(ch);

        // Not executing → resolves normally.
        await Assert.That(app.User.Channels.Get("logger")).IsEqualTo((Channel?)ch);

        // Simulate mid-execution by flipping the AsyncLocal directly.
        var field = typeof(GoalChannel).GetField("_executing",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var asyncLocal = (AsyncLocal<bool>)field.GetValue(ch)!;
        asyncLocal.Value = true;
        try
        {
            await Assert.That(app.User.Channels.Get("logger")).IsNull();
            await Assert.That(app.User.Channels.Resolve("logger")).IsNull();
        }
        finally { asyncLocal.Value = false; }

        // Restored: resolves again.
        await Assert.That(app.User.Channels.Get("logger")).IsEqualTo((Channel?)ch);
    }

    [Test]
    public async Task Channels_Get_LateRegisteredChannel_VisibleEverywhere()
    {
        // The bug we're closing: a channel registered after boot must be
        // visible even when lookups happen inside a goal-channel body.
        // With the old foundational-snapshot approach, late-registered names
        // were invisible there. With per-channel IsExecuting, they aren't.
        var app = new global::app.@this("/tmp/g_late");
        var sinkGoal = new EngineGoal { Name = "Sink", Path = "S.goal", PrPath = "/S.pr" };
        var sink = new GoalChannel("sink", sinkGoal, app.User);
        app.User.Channels.Register(sink);

        // Register "builder" AFTER "sink" exists. Old code froze foundational
        // before this; this test passes only because no freeze is involved.
        var builder = StreamChannel.Memory("builder");
        app.User.Channels.Register(builder);

        // Inside sink's body, "builder" must still resolve.
        var sinkExec = (AsyncLocal<bool>)typeof(GoalChannel)
            .GetField("_executing", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(sink)!;
        sinkExec.Value = true;
        try
        {
            await Assert.That(app.User.Channels.Get("builder")).IsEqualTo((Channel?)builder);
            // And "sink" itself is correctly hidden.
            await Assert.That(app.User.Channels.Get("sink")).IsNull();
        }
        finally { sinkExec.Value = false; }
    }

    [Test]
    public async Task GoalChannel_Ask_InvokesGoal_ReturnsAnswer()
    {
        var app = new global::app.@this("/tmp/g8");
        var goal = new EngineGoal { Name = "Asker", Path = "Asker.goal", PrPath = "/A.pr" };
        var ch = new GoalChannel("input", goal, app.User);
        var result = await ch.AskCore(new global::app.modules.output.ask { Question = new global::app.data.@this<string>("", "q?") });
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task GoalChannel_Dispose_DoesNotDisposeUnderlyingGoal()
    {
        var app = new global::app.@this("/tmp/g9");
        var goal = new EngineGoal { Name = "G", Path = "G.goal", PrPath = "/G.pr" };
        var ch = new GoalChannel("c", goal, app.User);
        await ch.DisposeAsync();
        // Goal still usable — re-register as a different channel.
        var ch2 = new GoalChannel("c2", goal, app.User);
        var result = await ch2.WriteCore(Data.Ok("x"));
        await Assert.That(result.Success).IsTrue();
    }
}
