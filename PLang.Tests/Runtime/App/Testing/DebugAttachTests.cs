namespace PLang.Tests.App.Tester;

// Debug attaches when the goal it names starts and follows what it calls — that stack, until it returns. With a step
// named too, it attaches at that step of the named goal and follows what the step calls.
public class DebugAttachTests
{
    private global::app.@this _app = null!;
    private global::app.channel.type.stream.@this _capture = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/test").Testing();
        _app.actor.list.System.Channel.Register(global::app.channel.type.stream.@this.Memory(global::app.channel.list.@this.Error));
        _capture = (global::app.channel.type.stream.@this)_app.actor.list.System.Channel.Get(global::app.channel.list.@this.Error)!;
    }

    [After(Test)]
    public async Task Teardown() => await _app.DisposeAsync();

    private string Shown()
    {
        _capture.Stream.Position = 0;
        using var reader = new StreamReader(_capture.Stream, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private async Task Debugging(Dictionary<string, object?> setting)
    {
        _app.actor.list.System.Setting.Set("debug.setting", setting);
        _app.Debug = new global::app.module.debug.@this(_app.actor.list.System.Context);
        await _app.Debug.Activate();
    }

    // A goal of formal steps, one line each, added to the app's goals.
    private async Task<global::app.goal.@this> Goal(string name, params string[] lines)
    {
        var ctx = _app.actor.list.User.Context;
        var steps = new List<Make.StepDef>();
        foreach (var text in lines)
        {
            var step = new global::app.goal.step.@this(_app.type.list) { Text = text };
            var read = await new global::app.goal.step.action.formal.Reader(step, _app.module.list).Read(text, ctx);
            steps.Add(Make.Step(text, ((global::app.goal.step.action.list.@this)read.Peek()!).Items().ToArray()));
        }
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(ctx, name, steps.ToArray()));
        _app.goal.list.Add(goal);
        return goal;
    }

    private static int Count(string shown, string text) => (shown.Length - shown.Replace(text, "").Length) / text.Length;

    [Test]
    public async Task TheNamedGoalsCalledGoals_AreDebugged()
    {
        await Goal("Inner", "variable.set(Name=%b%, Value=2)");
        var start = await Goal("Start", "goal.call(Name=\"Inner\")", "variable.set(Name=%a%, Value=1)");
        await Debugging(new() { ["goal"] = "Start" });

        await _app.Start(start, _app.actor.list.User.Context);

        var shown = Shown();
        await Assert.That(shown).Contains("Step [0] of Start");
        await Assert.That(shown).Contains("Step [1] of Start");
        await Assert.That(shown).Contains("Step [0] of Inner");
    }

    [Test]
    public async Task WithAStep_OnlyThatStepOfTheNamedGoal_AndWhatItCalls()
    {
        await Goal("Inner", "variable.set(Name=%b%, Value=2)", "variable.set(Name=%c%, Value=3)");
        var start = await Goal("Start", "variable.set(Name=%z%, Value=0)", "goal.call(Name=\"Inner\")", "variable.set(Name=%a%, Value=1)");
        await Debugging(new() { ["goal"] = "Start", ["step"] = 1 });

        await _app.Start(start, _app.actor.list.User.Context);

        var shown = Shown();
        await Assert.That(shown).Contains("Step [1] of Start");
        await Assert.That(shown).DoesNotContain("Step [0] of Start");
        await Assert.That(shown).DoesNotContain("Step [2] of Start");
        await Assert.That(shown).Contains("Step [0] of Inner");
        await Assert.That(shown).Contains("Step [1] of Inner");
    }

    // No goal named is every goal named: step N of each goal, and what that step calls.
    [Test]
    public async Task WithAStepAndNoGoal_StepNOfEveryGoal_AndWhatItCalls()
    {
        await Goal("Inner", "variable.set(Name=%b%, Value=2)", "variable.set(Name=%c%, Value=3)");
        var start = await Goal("Start", "variable.set(Name=%z%, Value=0)", "goal.call(Name=\"Inner\")");
        await Debugging(new() { ["step"] = 1 });

        await _app.Start(start, _app.actor.list.User.Context);

        var shown = Shown();
        await Assert.That(shown).Contains("Step [1] of Start");
        await Assert.That(shown).DoesNotContain("Step [0] of Start");
        await Assert.That(shown).Contains("Step [0] of Inner");
        await Assert.That(shown).Contains("Step [1] of Inner");
    }

    // A goal the named goal didn't call is not in its stack: Inner run by Beside is not debugged, Inner run by Start is.
    [Test]
    public async Task AGoalTheNamedOneDidNotCall_IsNotDebugged()
    {
        await Goal("Inner", "variable.set(Name=%b%, Value=2)");
        var beside = await Goal("Beside", "goal.call(Name=\"Inner\")");
        var start = await Goal("Start", "goal.call(Name=\"Inner\")");
        await Debugging(new() { ["goal"] = "Start" });

        await _app.Start(beside, _app.actor.list.User.Context);
        await Assert.That(Shown()).DoesNotContain("of Inner");

        await _app.Start(start, _app.actor.list.User.Context);
        await Assert.That(Shown()).Contains("Step [0] of Inner");
        await Assert.That(Shown()).DoesNotContain("of Beside");
    }

    // The named goal calling itself: what each run of it calls is debugged, the inner run's and the outer's.
    [Test]
    public async Task TheNamedGoalCallingItself_DebugsWhatEachRunCalls()
    {
        await Goal("Leaf", "variable.set(Name=%leaf%, Value=1)");
        var rec = await Goal("Rec",
            "condition.if(Left=%again%, Operator=\"==\", Right=true) { variable.set(Name=%again%, Value=false); goal.call(Name=\"Rec\") }",
            "goal.call(Name=\"Leaf\")");
        await _app.actor.list.User.Context.Variable.Set("again", true);
        await Debugging(new() { ["goal"] = "Rec" });

        await _app.Start(rec, _app.actor.list.User.Context);

        await Assert.That(Count(Shown(), "DEBUG [BEFORE]: Step [0] of Leaf")).IsEqualTo(2);
    }
}
