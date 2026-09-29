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
    public async Task GoalChannel_WriteCore_TheMessageEndsWithTheRun()
    {
        var app = new global::app.@this("/tmp/g1").Testing();
        var goal = new EngineGoal { Name = "Probe", Path = global::app.type.item.path.@this.Resolve("Probe.goal", app.actor.list.User.Context), PrPath = global::app.type.item.path.@this.Resolve("/Probe.pr", app.actor.list.User.Context) };
        app.goal.list.Add(goal);
        var ch = await Make.GoalChannel("logger", Make.Call(app.actor.list.User.Context, goal.Name), app.actor.list.User);
        var dataIn = app.Ok("payload-A");
        var result = await ch.Write(dataIn);
        await result.IsSuccess();

        // %message% is the goal's argument (read inside it: the test below); it isn't left behind
        await Assert.That((await app.actor.list.User.Context.Variable.Get("message")).IsInitialized).IsFalse();
    }

    // The goal reads what was written as its argument %message% — from its very first step.
    [Test]
    public async Task GoalChannel_TheGoalsFirstStep_ReadsTheWrittenMessage()
    {
        var app = new global::app.@this("/tmp/g_message").Testing();
        var goal = Make.Goal(app.actor.list.User.Context, "Sink", Make.Step("set %seen% = %message%",
            Make.Action(app.actor.list.User.Context, "variable", "set", Make.Param(app.actor.list.User.Context, "Name", "seen", "variable"), ("Value", "%message%"))));
        app.goal.list.Add(goal);
        var ch = await Make.GoalChannel("sink", Make.Call(app.actor.list.User.Context, goal.Name), app.actor.list.User);

        await (await ch.Write(app.Ok("Building path: /"))).IsSuccess();

        var seen = await app.actor.list.User.Context.Variable.Get("seen");
        await Assert.That((await seen.Value())?.ToString()).IsEqualTo("Building path: /");
    }

    [Test]
    public async Task GoalChannel_WriteCore_ReturnsGoalsResultData()
    {
        var app = new global::app.@this("/tmp/g2").Testing();
        var goal = new EngineGoal { Name = "ReturnsOk", Path = global::app.type.item.path.@this.Resolve("Returns.goal", app.actor.list.User.Context), PrPath = global::app.type.item.path.@this.Resolve("/R.pr", app.actor.list.User.Context) };
        app.goal.list.Add(goal);
        var ch = await Make.GoalChannel("c", Make.Call(app.actor.list.User.Context, goal.Name), app.actor.list.User);
        var result = await ch.Write(app.Ok("x"));
        await result.IsSuccess();
    }

    [Test]
    public async Task GoalChannel_IsExecuting_IsFalseBeforeAndAfterWrite()
    {
        var app = new global::app.@this("/tmp/g_exec").Testing();
        var goal = new EngineGoal { Name = "G", Path = global::app.type.item.path.@this.Resolve("G.goal", app.actor.list.User.Context), PrPath = global::app.type.item.path.@this.Resolve("/G.pr", app.actor.list.User.Context) };
        app.goal.list.Add(goal);
        var ch = await Make.GoalChannel("x", Make.Call(app.actor.list.User.Context, goal.Name), app.actor.list.User);
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
        var app = new global::app.@this("/tmp/g_recurse").Testing();
        var goal = new EngineGoal { Name = "G", Path = global::app.type.item.path.@this.Resolve("G.goal", app.actor.list.User.Context), PrPath = global::app.type.item.path.@this.Resolve("/G.pr", app.actor.list.User.Context) };
        app.goal.list.Add(goal);
        var ch = await Make.GoalChannel("logger", Make.Call(app.actor.list.User.Context, goal.Name), app.actor.list.User);
        app.actor.list.User.Channel.Register(ch);

        // Not executing → resolves normally.
        await Assert.That(app.actor.list.User.Channel.Get("logger")).IsEqualTo((Channel?)ch);

        // Simulate mid-execution by flipping the AsyncLocal directly.
        var field = typeof(GoalChannel).GetField("_executing",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var asyncLocal = (AsyncLocal<bool>)field.GetValue(ch)!;
        asyncLocal.Value = true;
        try
        {
            await Assert.That(app.actor.list.User.Channel.Get("logger")).IsNull();
            await Assert.That(app.actor.list.User.Channel.Get("logger")).IsNull();
        }
        finally { asyncLocal.Value = false; }

        // Restored: resolves again.
        await Assert.That(app.actor.list.User.Channel.Get("logger")).IsEqualTo((Channel?)ch);
    }

    [Test]
    public async Task Channels_Get_LateRegisteredChannel_VisibleEverywhere()
    {
        // The bug we're closing: a channel registered after boot must be
        // visible even when lookups happen inside a goal-channel body.
        // With the old foundational-snapshot approach, late-registered names
        // were invisible there. With per-channel IsExecuting, they aren't.
        var app = new global::app.@this("/tmp/g_late").Testing();
        var sinkGoal = new EngineGoal { Name = "Sink", Path = global::app.type.item.path.@this.Resolve("S.goal", app.actor.list.User.Context), PrPath = global::app.type.item.path.@this.Resolve("/S.pr", app.actor.list.User.Context) };
        app.goal.list.Add(sinkGoal);
        var sink = await Make.GoalChannel("sink", Make.Call(app.actor.list.User.Context, sinkGoal.Name), app.actor.list.User);
        app.actor.list.User.Channel.Register(sink);

        // Register "builder" AFTER "sink" exists. Old code froze foundational
        // before this; this test passes only because no freeze is involved.
        var builder = StreamChannel.Memory("builder");
        app.actor.list.User.Channel.Register(builder);

        // Inside sink's body, "builder" must still resolve.
        var sinkExec = (AsyncLocal<bool>)typeof(GoalChannel)
            .GetField("_executing", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(sink)!;
        sinkExec.Value = true;
        try
        {
            await Assert.That(app.actor.list.User.Channel.Get("builder")).IsEqualTo((Channel?)builder);
            // And "sink" itself is correctly hidden.
            await Assert.That(app.actor.list.User.Channel.Get("sink")).IsNull();
        }
        finally { sinkExec.Value = false; }
    }

    [Test]
    public async Task GoalChannel_Ask_InvokesGoal_ReturnsAnswer()
    {
        var app = new global::app.@this("/tmp/g8").Testing();
        var goal = new EngineGoal { Name = "Asker", Path = global::app.type.item.path.@this.Resolve("Asker.goal", app.actor.list.User.Context), PrPath = global::app.type.item.path.@this.Resolve("/A.pr", app.actor.list.User.Context) };
        app.goal.list.Add(goal);
        var ch = await Make.GoalChannel("input", Make.Call(app.actor.list.User.Context, goal.Name), app.actor.list.User);
        var result = await ch.Ask(new global::app.module.output.ask(app.actor.list.User.Context) { Question = new global::app.data.@this<global::app.type.item.text.@this>("", "q?") });
        await result.IsSuccess();
    }

    [Test]
    public async Task GoalChannel_Dispose_DoesNotDisposeUnderlyingGoal()
    {
        var app = new global::app.@this("/tmp/g9").Testing();
        var goal = new EngineGoal { Name = "G", Path = global::app.type.item.path.@this.Resolve("G.goal", app.actor.list.User.Context), PrPath = global::app.type.item.path.@this.Resolve("/G.pr", app.actor.list.User.Context) };
        app.goal.list.Add(goal);
        var ch = await Make.GoalChannel("c", Make.Call(app.actor.list.User.Context, goal.Name), app.actor.list.User);
        await ch.DisposeAsync();
        // Goal still usable — re-register as a different channel.
        var ch2 = await Make.GoalChannel("c2", Make.Call(app.actor.list.User.Context, goal.Name), app.actor.list.User);
        var result = await ch2.Write(app.Ok("x"));
        await result.IsSuccess();
    }
}
