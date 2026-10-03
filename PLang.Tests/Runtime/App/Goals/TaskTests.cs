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
        await Load("Slow", Make.Step("sleep", Sleep(1000)), Make.Step("append task", Set("order", "%order%,task")),
            Make.Step("return order", Return("%order%")));
        var caller = await Load("Caller",
            Make.Step("call Slow in parallel, write to %task%", InParallel("Slow"), Set("task", "%!data%")),
            Make.Step("set order", Set("order", "caller")));

        await (await caller.Start(Ctx)).IsSuccess();
        var task = (await Ctx.Variable.Get("task")).Peek() as global::app.task.@this;
        await Assert.That(task).IsNotNull();
        var waited = await task!.Wait();

        // the step after the call ran before the task read %order% (its reads fall through to the caller's, live);
        // what the task wrote stays in it
        await Assert.That((await waited.Value())?.ToString()).IsEqualTo("caller,task");
        await Assert.That((await (await Ctx.Variable.Get("order")).Value())?.ToString()).IsEqualTo("caller");
    }

    private global::app.goal.step.action.@this Return(string value) => Make.Action(Ctx, "goal", "return", ("Data", value));

    // a task's writes stay in it — a member written into a value it read from its caller included; two tasks writing one
    // name never overwrite each other
    [Test]
    public async Task ATasksWrites_StayInIt_AndSiblingsNeverOverwriteEachOther()
    {
        await Ctx.Variable.Set("kept", new Dictionary<string, object?> { ["order"] = "caller" });
        await Load("A", Make.Step("set v", Set("v", "a")), Make.Step("set kept mark", Set("%kept.mark%", "a")),
            Make.Step("sleep", Sleep(500)), Make.Step("return v", Return("%v%")));
        await Load("B", Make.Step("set v", Set("v", "b")), Make.Step("return v", Return("%v%")));
        var a = await Started("A");
        var b = await Started("B");

        await Assert.That((await (await a.Wait()).Value())?.ToString()).IsEqualTo("a");
        await Assert.That((await (await b.Wait()).Value())?.ToString()).IsEqualTo("b");
        await Assert.That((await Ctx.Variable.Get("v")).IsInitialized).IsFalse();
        var kept = await Ctx.Variable.Get("kept");
        await Assert.That((await ((global::app.type.item.@this)(await kept.Value())!).Get(kept, "mark")).IsInitialized).IsFalse();
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
        await Load("Mark", Make.Step("sleep", Sleep(1000)), Make.Step("return actor", Return("%!context.Actor.Name%")));
        var task = await Started("Mark", ("Actor", "system"));

        await Assert.That(_app.actor.list.System.Task.list).Contains(task);
        await Assert.That(_app.actor.list.User.Task.list).DoesNotContain(task);

        await Assert.That((await (await task.Wait()).Value())?.ToString()).IsEqualTo("System");
    }

    private global::app.goal.step.action.@this Add(string list, string value)
        => Make.Action(Ctx, "list", "add", Make.Param(Ctx, "ListName", list, "variable"), ("Value", value));

    private async Task<List<string?>> Texts(global::app.data.@this held)
    {
        var rows = new List<string?>();
        foreach (var row in ((global::app.type.item.list.@this)(await held.Value())!).Items(Ctx)) rows.Add((await row.Value())?.ToString());
        return rows;
    }

    // a list a task read from its caller and added to is the task's own copy: the caller's is as it was
    [Test]
    public async Task AnAddInATask_ToItsCallersList_LeavesTheCallersAsItWas()
    {
        await Ctx.Variable.Set("items", new List<object?> { "a" });
        await Load("Adds", Make.Step("add x", Add("items", "x")), Make.Step("return items", Return("%items%")));

        var task = await Started("Adds");
        var waited = await task.Wait();

        await Assert.That(await Texts(waited)).IsEquivalentTo(new[] { "a", "x" });
        await Assert.That(await Texts(await Ctx.Variable.Get("items"))).IsEquivalentTo(new[] { "a" });
    }

    // a plain call inside a task runs in the task's context: what the called goal writes stays in the task, and the
    // task's next step reads it
    [Test]
    public async Task APlainCallInsideATask_RunsInTheTask_ItsWritesStayThere()
    {
        await Load("Writes", Make.Step("set w", Set("w", "x")));
        await Load("Calls", Make.Step("call Writes", Make.Call(Ctx, "Writes")), Make.Step("return w", Return("%w%")));

        var waited = await (await Started("Calls")).Wait();

        await Assert.That((await waited.Value())?.ToString()).IsEqualTo("x");
        await Assert.That((await Ctx.Variable.Get("w")).IsInitialized).IsFalse();
    }

    // a task setting a name its caller's frame keeps (the caller's parameter) writes its own: the caller's is unchanged
    [Test]
    public async Task ATaskSettingANameItsCallersFrameKeeps_LeavesTheCallersValue()
    {
        await Load("SetsCity", Make.Step("set city", Set("city", "task's")), Make.Step("return city", Return("%city%")));
        await Load("Caller", Make.Step("call SetsCity in parallel, write to %task%", InParallel("SetsCity"), Set("task", "%!data%")),
            Make.Step("wait for it", Make.Action(Ctx, "task", "wait", ("Task", "%task%"))),
            Make.Step("return city", Return("%city%")));
        var outer = await Load("Outer", Make.Step("call Caller city=caller's, write to %got%",
            Make.Call(Ctx, "Caller", ("city", "caller's")), Set("got", "%!data%")));

        await (await outer.Start(Ctx)).IsSuccess();

        await Assert.That((await (await Ctx.Variable.Get("got")).Value())?.ToString()).IsEqualTo("caller's");
    }

    // a typed list copies as itself: the task's copy is a list<text>, the caller's untouched
    [Test]
    public async Task AnAddInATask_ToItsCallersTypedList_CopiesItAsATypedList()
    {
        await Ctx.Variable.Set("items", new global::app.type.item.list.@this<global::app.type.item.text.@this>(
            new global::app.type.item.@this[] { new global::app.type.item.text.@this("a") }));
        await Load("Adds", Make.Step("add x", Add("items", "x")), Make.Step("return items", Return("%items%")));

        var waited = await (await Started("Adds")).Wait();

        await Assert.That(waited.Peek()).IsTypeOf<global::app.type.item.list.@this<global::app.type.item.text.@this>>();
        await Assert.That(await Texts(waited)).IsEquivalentTo(new[] { "a", "x" });
        await Assert.That(await Texts(await Ctx.Variable.Get("items"))).IsEquivalentTo(new[] { "a" });
    }

    [Test]
    public async Task TwoSiblingsAddingToTheirCallersList_NeverSeeEachOthersAdditions()
    {
        await Ctx.Variable.Set("items", new List<object?> { "a" });
        await Load("AddsB", Make.Step("add b", Add("items", "b")), Make.Step("sleep", Sleep(300)), Make.Step("return items", Return("%items%")));
        await Load("AddsC", Make.Step("add c", Add("items", "c")), Make.Step("sleep", Sleep(300)), Make.Step("return items", Return("%items%")));

        var b = await Started("AddsB");
        var c = await Started("AddsC");

        await Assert.That(await Texts(await b.Wait())).IsEquivalentTo(new[] { "a", "b" });
        await Assert.That(await Texts(await c.Wait())).IsEquivalentTo(new[] { "a", "c" });
        await Assert.That(await Texts(await Ctx.Variable.Get("items"))).IsEquivalentTo(new[] { "a" });
    }

    // A deadline is its own flow's: a timeout in one task never cancels a sibling running beside it on the same actor.
    [Test]
    public async Task ATimeoutInOneTask_DoesNotCancelItsSibling()
    {
        // TimesOut can't end on its own: its deadline is the only way out. The sibling's sleep reads its own token, which
        // nothing cancels, so it always ends "done". Load slows this, never flips it. (That the two never share a token
        // is the next pin: each runs in a context of its own.)
        await Load("TimesOut", Make.Step("sleep, timeout after 1000ms", Sleep(60_000),
            Make.Action(Ctx, "on", "timeout", ("After", System.TimeSpan.FromMilliseconds(1000)))));
        await Load("Sibling", Make.Step("sleep", Sleep(2000)), Make.Step("return done", Return("done")));

        var timesOut = await Started("TimesOut");
        var sibling = await Started("Sibling");

        await Assert.That((await timesOut.Wait()).Error?.Key).IsEqualTo("Timeout");
        await Assert.That((await (await sibling.Wait()).Value())?.ToString()).IsEqualTo("done");
    }

    // each task runs in a context of its own — not its caller's, not its sibling's: its memory, its cancellation (a
    // deadline one pushes is never another's)
    [Test]
    public async Task TwoTasks_EachRunInAContextOfItsOwn()
    {
        await Load("Where", Make.Step("return the context", Return("%!context.Id%")));

        var first = await (await Started("Where")).Wait();
        var second = await (await Started("Where")).Wait();

        var ids = new[] { Ctx.Id, (await first.Value())?.ToString(), (await second.Value())?.ToString() };
        await Assert.That(ids.Distinct().Count()).IsEqualTo(3);
    }

    private async Task<global::app.data.@this> Action(string module, string action, params (string name, object? value)[] properties)
        => await Make.Action(Ctx, module, action, properties).Start(Ctx);

    [Test]
    public async Task Cancel_StopsTheGoal_AnswersNothing_AndALaterWaitFailsCancelled_NeverReported()
    {
        await Load("Slow", Make.Step("sleep", Sleep(1000)), Make.Step("set after", Set("after", 1)));
        await Started("Slow");

        var cancelled = await Action("task", "cancel", ("Task", "%task%"));

        await cancelled.IsSuccess();
        await Assert.That(cancelled.Peek().IsNull).IsTrue();
        var waited = await Action("task", "wait", ("Task", "%task%"));
        await Assert.That(waited.Error?.Key).IsEqualTo("Cancelled");
        await Assert.That((await Ctx.Variable.Get("after")).IsInitialized).IsFalse();
        await Assert.That(Errors).DoesNotContain("cancelled");
    }

    [Test]
    public async Task CancelOfAnEndedTask_AnswersItsResult()
    {
        await Load("Quick", Make.Step("return done", Make.Action(Ctx, "goal", "return", ("Data", "done"))));
        var task = await Started("Quick");
        await task.Wait();

        var cancelled = await Action("task", "cancel", ("Task", "%task%"));

        await Assert.That((await cancelled.Value())?.ToString()).IsEqualTo("done");
    }

    [Test]
    public async Task WaitForATask_AnswersItsResult()
    {
        await Load("Quick", Make.Step("sleep", Sleep(100)), Make.Step("return done", Make.Action(Ctx, "goal", "return", ("Data", "done"))));
        await Started("Quick");

        var waited = await Action("task", "wait", ("Task", "%task%"));

        await Assert.That((await waited.Value())?.ToString()).IsEqualTo("done");
    }

    [Test]
    public async Task WaitForAFailedTask_FailsTheStep()
    {
        await Load("Fails", Make.Step("throw", Make.Action(Ctx, "error", "throw", ("Message", "it broke"), ("Key", "Broke"))));
        await Started("Fails");

        var waited = await Action("task", "wait", ("Task", "%task%"));

        await Assert.That(waited.Error?.Key).IsEqualTo("Broke");
    }

    // a task written where a task was keeps it: %task.list% is every task written there, in order, the last %task%;
    // wait for %task.list% answers their results in that order; the actor lists each run once
    [Test]
    public async Task ATaskWrittenOverATask_KeepsIt_AndWaitForTheListAnswersAllInOrder()
    {
        await Load("First", Make.Step("sleep", Sleep(300)), Make.Step("return first", Make.Action(Ctx, "goal", "return", ("Data", "first"))));
        await Load("Second", Make.Step("return second", Make.Action(Ctx, "goal", "return", ("Data", "second"))));
        var caller = await Load("Caller",
            Make.Step("call First in parallel, write to %task%", InParallel("First"), Set("task", "%!data%")),
            Make.Step("call Second in parallel, write to %task%", InParallel("Second"), Set("task", "%!data%")));
        await (await caller.Start(Ctx)).IsSuccess();

        var task = (global::app.task.@this)(await Ctx.Variable.Get("task")).Peek()!;
        var written = task.list.Items(Ctx).Select(row => (global::app.task.@this)row.Peek()!).ToList();
        await Assert.That(written.Count).IsEqualTo(2);
        await Assert.That(written[1]).IsSameReferenceAs(task);
        await Assert.That(written[0].Goal.Name).IsEqualTo("First");
        await Assert.That(_app.actor.list.User.Task.list.Count()).IsLessThanOrEqualTo(2);

        var waited = await Action("task", "wait", ("List", "%task.list%"));

        var results = ((global::app.type.item.list.@this)(await waited.Value())!).Items(Ctx).ToList();
        await Assert.That((await results[0].Value())?.ToString()).IsEqualTo("first");
        await Assert.That((await results[1].Value())?.ToString()).IsEqualTo("second");
    }

    // every other value replaces as it did: nothing written before it is kept
    [Test]
    public async Task ATextOrADictWrittenOverItsKind_Replaces()
    {
        await Ctx.Variable.Set("t", "one");
        await Ctx.Variable.Set("t", "two");
        await Ctx.Variable.Set("d", new Dictionary<string, object?> { ["a"] = 1 });
        await Ctx.Variable.Set("d", new Dictionary<string, object?> { ["b"] = 2 });

        await Assert.That((await (await Ctx.Variable.Get("t")).Value())?.ToString()).IsEqualTo("two");
        var d = await Ctx.Variable.Get("d");
        var dict = (global::app.type.item.@this)(await d.Value())!;
        await Assert.That((await dict.Get(d, "a")).IsInitialized).IsFalse();
        await Assert.That((await dict.Get(d, "b")).IsInitialized).IsTrue();
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

    // ---- foreach in parallel ----

    // Loop: foreach %items% in parallel(<parallel>), call Each item=%item%, write to %task% — then the step after it
    private async Task<global::app.task.@this> Looped(object parallel)
    {
        await Ctx.Variable.Set("items", new List<object?> { "a", "b", "c" });
        var loop = await Load("Loop",
            Make.Step("foreach %items% in parallel, call Each, write to %task%",
                Make.Action(Ctx, "loop", "foreach", ("collection", "%items%"), Make.Param(Ctx, "item", "%item%", "variable"), ("Parallel", parallel)),
                Make.Call(Ctx, "Each"), Set("task", "%!data%")),
            Make.Step("set after", Set("after", "ran")));
        await (await loop.Start(Ctx)).IsSuccess();
        return (global::app.task.@this)(await Ctx.Variable.Get("task")).Peek()!;
    }

    [Test]
    public async Task AForeachInParallel_AnswersATask_AndItsResultIsCountAndComplete()
    {
        await Load("Each", Make.Step("sleep", Sleep(300)));

        var task = await Looped(true);

        // the step after the loop ran before its items ended
        await Assert.That(task.Ended.Value).IsFalse();
        await Assert.That((await (await Ctx.Variable.Get("after")).Value())?.ToString()).IsEqualTo("ran");
        var waited = await task.Wait();
        await waited.IsSuccess();
        var result = (global::app.type.item.dict.@this)(await waited.Value())!;
        await Assert.That(result.Get("count", Ctx)!.Peek()!.ToString()).IsEqualTo("3");
        await Assert.That(result.Get("complete", Ctx)!.Peek()!.ToString()).IsEqualTo("true");
    }

    // write to after a foreach keeps the loop's answer: one after another, {count, complete} — never the last call's
    [Test]
    public async Task AForeachsWriteTo_KeepsTheLoopsAnswer_NotTheLastCalls()
    {
        await Load("Each", Make.Step("return the item", Make.Action(Ctx, "goal", "return", ("Data", "%item%"))));
        await Ctx.Variable.Set("items", new List<object?> { "a", "b", "c" });
        var loop = await Load("Loop",
            Make.Step("foreach %items%, call Each, write to %r%",
                Make.Action(Ctx, "loop", "foreach", ("collection", "%items%"), Make.Param(Ctx, "item", "%item%", "variable")),
                Make.Call(Ctx, "Each"), Set("r", "%!data%")));

        await (await loop.Start(Ctx)).IsSuccess();

        var result = (global::app.type.item.dict.@this)(await (await Ctx.Variable.Get("r")).Value())!;
        await Assert.That(result.Get("count", Ctx)!.Peek()!.ToString()).IsEqualTo("3");
        await Assert.That(result.Get("complete", Ctx)!.Peek()!.ToString()).IsEqualTo("true");
    }

    // the builder writes no Item row for the default %item%: a loop read from such a .pr binds %item%, one after
    // another and in parallel
    [Test]
    public async Task AForeachWithNoItemRow_BindsItem_OneAfterAnother()
    {
        await Load("Each", Make.Step("keep the item", Set("seen", "%item%")));
        await Ctx.Variable.Set("items", new List<object?> { "a", "b", "c" });
        var loop = await Load("Loop",
            Make.Step("foreach %items%, call Each",
                Make.Action(Ctx, "loop", "foreach", ("collection", "%items%")), Make.Call(Ctx, "Each")));

        await (await loop.Start(Ctx)).IsSuccess();

        await Assert.That((await (await Ctx.Variable.Get("seen")).Value())?.ToString()).IsEqualTo("c");
    }

    [Test]
    public async Task AForeachWithNoItemRow_BindsItem_InParallel()
    {
        await Load("Each", Make.Step("return the item", Make.Action(Ctx, "goal", "return", ("Data", "%item%"))));
        await Ctx.Variable.Set("items", new List<object?> { "a", "b", "c" });
        var loop = await Load("Loop",
            Make.Step("foreach %items% in parallel, call Each, write to %task%",
                Make.Action(Ctx, "loop", "foreach", ("collection", "%items%"), ("Parallel", true)),
                Make.Call(Ctx, "Each"), Set("task", "%!data%")));

        await (await loop.Start(Ctx)).IsSuccess();
        var waited = await ((global::app.task.@this)(await Ctx.Variable.Get("task")).Peek()!).Wait();

        await waited.IsSuccess();
        var result = (global::app.type.item.dict.@this)(await waited.Value())!;
        await Assert.That(result.Get("count", Ctx)!.Peek()!.ToString()).IsEqualTo("3");
    }

    // in parallel the same write keeps the loop's task
    [Test]
    public async Task AForeachInParallelsWriteTo_KeepsTheLoopsTask()
    {
        await Load("Each", Make.Step("return the item", Make.Action(Ctx, "goal", "return", ("Data", "%item%"))));

        var task = await Looped(true);

        await Assert.That(task).IsTypeOf<global::app.task.@this>();
        await (await task.Wait()).IsSuccess();
    }

    // cpu caps how many run at once: at one, three 300 ms items take at least 900 ms
    [Test]
    public async Task AForeachInParallel_RunsAtMostCpuAtOnce()
    {
        await Load("Each", Make.Step("sleep", Sleep(300)));
        var clock = System.Diagnostics.Stopwatch.StartNew();

        await (await (await Looped(1)).Wait()).IsSuccess();

        await Assert.That(clock.ElapsedMilliseconds).IsGreaterThanOrEqualTo(880);
    }

    [Test]
    public async Task AnItemsFailure_IsTheLoopsTaskResult()
    {
        await Load("Each", Make.Step("fail", Make.Action(Ctx, "error", "throw", ("Message", "bad item"))));

        var waited = await (await Looped(true)).Wait();

        await waited.IsFailure();
        await Assert.That(waited.Error!.Message).Contains("bad item");
    }

    // parallel is a value: true is its default, false is not parallel, a number is how many at once, {cpu} says it
    [Test]
    public async Task Parallel_IsMadeFromWhatAProgramWrites()
    {
        global::app.type.item.parallel.@this? Made(object? raw)
            => global::app.type.item.parallel.@this.Create(raw, null, new global::app.data.@this("p", context: Ctx));
        var cores = System.Math.Max(1, (long)(System.Environment.ProcessorCount * 0.8));

        await Assert.That(Made(true)!.IsTruthy()).IsTrue();
        await Assert.That(Made(true)!.Cpu.ToInt64()).IsEqualTo(cores);
        await Assert.That(Made(false)!.IsTruthy()).IsFalse();
        await Assert.That(Made(2L)!.Cpu.ToInt64()).IsEqualTo(2L);
        await Assert.That(Made(0L)!.Cpu.ToInt64()).IsEqualTo(1L);
        var dict = (global::app.type.item.dict.@this)(await new global::app.data.@this("d",
            new Dictionary<string, object?> { ["cpu"] = 3L }, context: Ctx).Value())!;
        await Assert.That(Made(dict)!.Cpu.ToInt64()).IsEqualTo(3L);
    }
}
