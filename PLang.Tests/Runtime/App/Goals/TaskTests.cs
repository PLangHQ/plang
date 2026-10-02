using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Goals;

/// <summary>
/// `call X in parallel` (goal.call Parallel): the call answers a task and the step goes on; the task is listed under the
/// actor that runs it while it runs, and leaves the list when it ends. Its result is reached through Wait; a failure
/// nobody waited for goes to the actor's error channel, and a later Wait still answers it. What ran before what is read
/// from what the goals wrote, never from durations.
/// </summary>
public class TaskTests
{
    private global::app.@this _app = null!;
    private System.IO.MemoryStream _errors = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/tmp/task-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        _errors = new System.IO.MemoryStream();
        _app.actor.list.User.Channel.Register(new global::app.channel.type.stream.@this(
            global::app.channel.list.@this.Error, _errors, global::app.channel.ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain", Framed = true });
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private async Task<global::app.goal.@this> Load(string name, params Make.StepDef[] steps)
    {
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(_app, Make.Goal(Ctx, name, "/" + name + ".goal", steps));
        _app.goal.list.Add(goal);
        return goal;
    }

    private global::app.goal.step.action.@this Set(string name, object? value)
        => Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", name, "variable"), ("Value", value));

    private global::app.goal.step.action.@this Sleep(int ms) => Make.Action(Ctx, "timer", "sleep", ("Ms", ms));

    private global::app.goal.step.action.@this InParallel(string goal, params (string name, object? value)[] more)
        => Make.Action(Ctx, "goal", "call", new[] { ("Name", (object?)goal), ("Parallel", (object?)true) }.Concat(more).ToArray());

    // Caller: call <goal> in parallel, write to %task% — and the task it answered
    private async Task<global::app.task.@this> Started(string goal, params (string name, object? value)[] more)
    {
        var caller = await Load("Caller", Make.Step($"call {goal} in parallel, write to %task%", InParallel(goal, more), Set("task", "%!data%")));
        await (await caller.Start(Ctx)).IsSuccess();
        return (global::app.task.@this)(await Ctx.Variable.Get("task")).Peek()!;
    }

    private string Errors => System.Text.Encoding.UTF8.GetString(_errors.ToArray());

    // a condition met within a few seconds
    private static async Task<bool> Soon(System.Func<bool> met)
    {
        for (var i = 0; i < 100; i++) { if (met()) return true; await Task.Delay(50); }
        return false;
    }

    [Test]
    public async Task AParallelCall_AnswersATask_AndTheNextStepRunsBeforeTheGoalEnds()
    {
        await Load("Slow", Make.Step("sleep", Sleep(1000)), Make.Step("append task", Set("order", "%order%,task")));
        var caller = await Load("Caller",
            Make.Step("call Slow in parallel, write to %task%", InParallel("Slow"), Set("task", "%!data%")),
            Make.Step("set order", Set("order", "caller")));

        await (await caller.Start(Ctx)).IsSuccess();
        var task = (await Ctx.Variable.Get("task")).Peek() as global::app.task.@this;
        await Assert.That(task).IsNotNull();
        await (await task!.Wait()).IsSuccess();

        await Assert.That((await (await Ctx.Variable.Get("order")).Value())?.ToString()).IsEqualTo("caller,task");
    }

    [Test]
    public async Task APlainCall_AnswersTheGoalsResult_AndStartsNoTask()
    {
        await Load("Callee", Make.Step("return done", Make.Action(Ctx, "goal", "return", ("Data", "done"))));
        var caller = await Load("Caller", Make.Step("call Callee, write to %got%", Make.Call(Ctx, "Callee"), Set("got", "%!data%")));

        await (await caller.Start(Ctx)).IsSuccess();

        await Assert.That((await (await Ctx.Variable.Get("got")).Value())?.ToString()).IsEqualTo("done");
        await Assert.That(_app.actor.list.User.Task.list.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task ARunningTask_IsListedUnderItsActor_AndLeavesWhenItEnds_ItsVariableStillHoldingIt()
    {
        await Load("Slow", Make.Step("sleep", Sleep(1000)));
        var task = await Started("Slow");

        await Assert.That(_app.actor.list.User.Task.list).Contains(task);
        var byId = await _app.actor.list.User.Task.Get(Ctx.Ok(_app.actor.list.User.Task), task.Id.ToString()!);
        await Assert.That(byId.Peek()).IsSameReferenceAs(task);
        await Assert.That(task.Ended.Value).IsFalse();

        await task.Wait();

        await Assert.That(_app.actor.list.User.Task.list).DoesNotContain(task);
        await Assert.That(task.Ended.Value).IsTrue();
        await Assert.That((await Ctx.Variable.Get("task")).Peek()).IsSameReferenceAs(task);
    }

    [Test]
    public async Task AFailureNobodyWaitedFor_GoesToTheErrorChannel_AndALaterWaitStillAnswersIt()
    {
        await Load("Fails", Make.Step("throw", Make.Action(Ctx, "error", "throw", ("Message", "the background broke"), ("Key", "BackgroundBroke"))));
        var task = await Started("Fails");

        await Assert.That(await Soon(() => Errors.Contains("the background broke"))).IsTrue();

        var waited = await task.Wait();
        await waited.IsFailure();
        await Assert.That(waited.Error!.Key).IsEqualTo("BackgroundBroke");
    }

    [Test]
    public async Task AFailureWaitedFor_IsTheWaitsAnswer_AndIsNotReported()
    {
        await Load("Fails", Make.Step("sleep", Sleep(1000)),
            Make.Step("throw", Make.Action(Ctx, "error", "throw", ("Message", "the waited one broke"), ("Key", "WaitedBroke"))));
        var task = await Started("Fails");

        var waited = await task.Wait();

        await waited.IsFailure();
        await Assert.That(waited.Error!.Key).IsEqualTo("WaitedBroke");
        await Assert.That(Errors).DoesNotContain("the waited one broke");
    }

    [Test]
    public async Task TheActorNamed_RunsIt_AndListsIt()
    {
        await Load("Mark", Make.Step("sleep", Sleep(1000)), Make.Step("set ran", Set("ran", 1)));
        var task = await Started("Mark", ("Actor", "system"));

        await Assert.That(_app.actor.list.System.Task.list).Contains(task);
        await Assert.That(_app.actor.list.User.Task.list).DoesNotContain(task);
        await (await task.Wait()).IsSuccess();

        await Assert.That((await _app.actor.list.System.Context.Variable.Get("ran")).IsInitialized).IsTrue();
        await Assert.That((await Ctx.Variable.Get("ran")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task AHundredTasks_AllEnded_LeaveTheActorsListEmpty()
    {
        await Load("Quick", Make.Step("set n", Set("n", 1)));
        var call = InParallel("Quick");
        var tasks = new List<global::app.task.@this>();
        for (var i = 0; i < 100; i++)
            tasks.Add((global::app.task.@this)(await call.Start(Ctx)).Peek()!);

        foreach (var task in tasks) await (await task.Wait()).IsSuccess();

        await Assert.That(_app.actor.list.User.Task.list.Count()).IsEqualTo(0);
    }
}
