using System.Reflection;
using GoalChannel = global::app.channel.type.goal.@this;
using EngineGoal = global::app.goal.@this;

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
        var app = global::PLang.Tests.TestApp.Create("/tmp/g1");
        var goal = new EngineGoal { Name = "Probe", Path = global::app.type.item.path.@this.Resolve("Probe.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/Probe.pr", global::PLang.Tests.TestApp.SharedContext) };
        var ch = new GoalChannel("logger", goal, app.User);
        var dataIn = app.Ok("payload-A");
        var result = await ch.Write(dataIn);
        await result.IsSuccess();

        var captured = await app.User.Context.Variable.Get("!data");
        await Assert.That(captured).IsNotNull();
    }

    [Test]
    public async Task GoalChannel_WriteCore_ReturnsGoalsResultData()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/g2");
        var goal = new EngineGoal { Name = "ReturnsOk", Path = global::app.type.item.path.@this.Resolve("Returns.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/R.pr", global::PLang.Tests.TestApp.SharedContext) };
        var ch = new GoalChannel("c", goal, app.User);
        var result = await ch.Write(app.Ok("x"));
        await result.IsSuccess();
    }

    [Test]
    public async Task GoalChannel_IsExecuting_IsFalseBeforeAndAfterWrite()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/g_exec");
        var goal = new EngineGoal { Name = "G", Path = global::app.type.item.path.@this.Resolve("G.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/G.pr", global::PLang.Tests.TestApp.SharedContext) };
        var ch = new GoalChannel("x", goal, app.User);
        await Assert.That(ch.IsExecuting).IsFalse();
        await ch.Write(app.Ok("x"));
        await Assert.That(ch.IsExecuting).IsFalse();
    }

    [Test]
    public async Task Channels_Get_TreatsExecutingGoalChannelAsNotFound()
    {
        // The load-bearing recursion guard: while a goal-channel's body is
        // running, the registry treats that name as not-found, so a body that
        // writes to its own name can't loop back into itself.
        var app = global::PLang.Tests.TestApp.Create("/tmp/g_recurse");
        var goal = new EngineGoal { Name = "G", Path = global::app.type.item.path.@this.Resolve("G.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/G.pr", global::PLang.Tests.TestApp.SharedContext) };
        var ch = new GoalChannel("logger", goal, app.User);
        app.User.Channel.Register(ch);

        // Not executing → resolves normally.
        await Assert.That(app.User.Channel.Get("logger")).IsEqualTo((Channel?)ch);

        // Simulate mid-execution by flipping the AsyncLocal directly.
        var field = typeof(GoalChannel).GetField("_executing",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var asyncLocal = (AsyncLocal<bool>)field.GetValue(ch)!;
        asyncLocal.Value = true;
        try
        {
            await Assert.That(app.User.Channel.Get("logger")).IsNull();
            await Assert.That(app.User.Channel.Resolve("logger")).IsNull();
        }
        finally { asyncLocal.Value = false; }

        // Restored: resolves again.
        await Assert.That(app.User.Channel.Get("logger")).IsEqualTo((Channel?)ch);
    }

    [Test]
    public async Task Channels_Get_LateRegisteredChannel_VisibleEverywhere()
    {
        // The bug we're closing: a channel registered after boot must be
        // visible even when lookups happen inside a goal-channel body.
        // With the old foundational-snapshot approach, late-registered names
        // were invisible there. With per-channel IsExecuting, they aren't.
        var app = global::PLang.Tests.TestApp.Create("/tmp/g_late");
        var sinkGoal = new EngineGoal { Name = "Sink", Path = global::app.type.item.path.@this.Resolve("S.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/S.pr", global::PLang.Tests.TestApp.SharedContext) };
        var sink = new GoalChannel("sink", sinkGoal, app.User);
        app.User.Channel.Register(sink);

        // Register "builder" AFTER "sink" exists. Old code froze foundational
        // before this; this test passes only because no freeze is involved.
        var builder = StreamChannel.Memory("builder");
        app.User.Channel.Register(builder);

        // Inside sink's body, "builder" must still resolve.
        var sinkExec = (AsyncLocal<bool>)typeof(GoalChannel)
            .GetField("_executing", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(sink)!;
        sinkExec.Value = true;
        try
        {
            await Assert.That(app.User.Channel.Get("builder")).IsEqualTo((Channel?)builder);
            // And "sink" itself is correctly hidden.
            await Assert.That(app.User.Channel.Get("sink")).IsNull();
        }
        finally { sinkExec.Value = false; }
    }

    [Test]
    public async Task GoalChannel_Ask_InvokesGoal_ReturnsAnswer()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/g8");
        var goal = new EngineGoal { Name = "Asker", Path = global::app.type.item.path.@this.Resolve("Asker.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/A.pr", global::PLang.Tests.TestApp.SharedContext) };
        var ch = new GoalChannel("input", goal, app.User);
        var result = await ch.Ask(new global::app.module.action.output.ask(app.User.Context) { Question = new global::app.data.@this<global::app.type.item.text.@this>("", "q?") });
        await result.IsSuccess();
    }

    [Test]
    public async Task GoalChannel_Dispose_DoesNotDisposeUnderlyingGoal()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/g9");
        var goal = new EngineGoal { Name = "G", Path = global::app.type.item.path.@this.Resolve("G.goal", global::PLang.Tests.TestApp.SharedContext), PrPath = global::app.type.item.path.@this.Resolve("/G.pr", global::PLang.Tests.TestApp.SharedContext) };
        var ch = new GoalChannel("c", goal, app.User);
        await ch.DisposeAsync();
        // Goal still usable — re-register as a different channel.
        var ch2 = new GoalChannel("c2", goal, app.User);
        var result = await ch2.Write(app.Ok("x"));
        await result.IsSuccess();
    }
}
