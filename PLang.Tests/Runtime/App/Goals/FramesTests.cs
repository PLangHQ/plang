namespace PLang.Tests.App.Goals;

/// <summary>
/// A goal, a step and an action each run in their own call-stack frame, and the goal and step in play are
/// the current frame's (<c>CallStack.Goal</c>, <c>CallStack.Step</c>) — nothing else holds them. A frame
/// answers the time it has run while it is still running, so an after-binding reads its step's time.
/// </summary>
public class FramesTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/tmp/frames-" + System.Guid.NewGuid().ToString("N")[..8]);

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private async Task<global::app.goal.@this> Load(string name, params global::PLang.Tests.Shared.Make.StepDef[] steps)
        => await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(_app,
            global::PLang.Tests.Shared.Make.Goal(name, "/" + name + ".goal", steps));

    private global::app.goal.step.action.@this Set(string name, object? value)
        => global::PLang.Tests.Shared.Make.Action("variable", "set",
            global::PLang.Tests.Shared.Make.Param("Name", name, "variable"), ("Value", value));

    // Caller's step 0 calls Callee; Callee runs its own steps; caller's step 0 then ends.
    private async Task<(global::app.goal.@this caller, global::app.goal.@this callee)> CallerAndCallee()
    {
        var callee = await Load("Callee",
            global::PLang.Tests.Shared.Make.Step("set a", Set("a", 1)),
            global::PLang.Tests.Shared.Make.Step("set b", Set("b", 2)));
        var caller = await Load("Caller",
            global::PLang.Tests.Shared.Make.Step("call Callee", global::PLang.Tests.Shared.Make.Action("goal", "call", ("Name", "Callee"))),
            global::PLang.Tests.Shared.Make.Step("set c", Set("c", 3)));
        _app.goal.list.Add(callee);
        _app.goal.list.Add(caller);
        return (caller, callee);
    }

    [Test]
    public async Task AfterAStepThatCallsAGoal_TheStepInPlay_IsTheCallers()
    {
        var (caller, _) = await CallerAndCallee();
        var ctx = _app.actor.list.User.Context;

        // what the step type's after-binding sees, per step of the caller
        var seen = new List<(global::app.goal.step.@this ending, global::app.goal.step.@this? inPlay, global::app.goal.@this? goal)>();
        _app.type.list["step"].Own().Bind("start", global::app.@event.When.after, (item, _, c) =>
        {
            if (item is global::app.goal.step.@this step && ReferenceEquals(step.Goal, caller))
                seen.Add((step, c.CallStack.Step, c.CallStack.Goal));
            return Task.FromResult(c.Ok());
        }, _app.actor.list.User, global::app.@event.binding.Scope.actor);

        var result = await caller.Start(ctx);

        await result.IsSuccess();
        await Assert.That(seen.Count).IsEqualTo(2);
        foreach (var (ending, inPlay, goal) in seen)
        {
            await Assert.That(inPlay).IsSameReferenceAs(ending);
            await Assert.That(goal).IsSameReferenceAs(caller);
        }
        // and once the goal ends, nothing it ran is still in play
        await Assert.That(ctx.CallStack.Step).IsNull();
        await Assert.That(ctx.CallStack.Goal).IsNull();
    }

    [Test]
    public async Task AStepsAfterBinding_ReadsItsTimeOffItsFrame()
    {
        await _app.actor.list.System.Setting.Set(new global::app.callstack.setting.@this().Path + ".timing", _app.actor.list.System.Context.Ok(true));
        var (caller, _) = await CallerAndCallee();

        var took = new List<TimeSpan?>();
        _app.type.list["step"].Own().Bind("start", global::app.@event.When.after, (item, _, c) =>
        {
            if (item is global::app.goal.step.@this step && ReferenceEquals(step.Goal, caller))
                took.Add(c.CallStack.Current?.Duration);
            return Task.FromResult(c.Ok());
        }, _app.actor.list.User, global::app.@event.binding.Scope.actor);

        await (await caller.Start(_app.actor.list.User.Context)).IsSuccess();

        await Assert.That(took.Count).IsEqualTo(2);
        foreach (var t in took)
            await Assert.That(t).IsNotNull();
    }

    [Test]
    public async Task ADebugStackLine_NamesTheGoalStepAndAction()
    {
        var (caller, _) = await CallerAndCallee();
        var stack = _app.actor.list.User.CallStack;
        var step = caller.Step[0];

        await using var goalFrame = stack.Push(caller);
        await using var stepFrame = stack.Push(step);
        await using var actionFrame = stack.Push(step.Code[0]);

        await Assert.That(goalFrame.ToString()).IsEqualTo("Caller in /Caller.goal");
        await Assert.That(stepFrame.ToString()).IsEqualTo("Caller (step 1) in /Caller.goal");
        await Assert.That(actionFrame.ToString()).IsEqualTo("Caller.call (step 1) in /Caller.goal");
    }
}
