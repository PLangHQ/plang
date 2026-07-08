using app.actor.context;
using app.variable;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Replicates the builder's compile write: `set %goal.Steps[i].Actions% = %compileResult.actions%`
/// where the compiled actions ride as a clr(json) array. They must land on the Step's typed
/// Actions (list&lt;action&gt;), not be lost — the "no actions" builder blocker.
/// </summary>
public class StepActionsWriteTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [Test]
    public async Task WriteClrJsonActions_LandsOnStepActions()
    {
        var context = _app.User.Context;
        var goal = new Goal
        {
            Name = "X",
            Path = global::app.type.path.@this.Resolve("/X.goal", context),
            Steps = new GoalSteps { new Step { Index = 0, Text = "write hello" } }
        };
        await context.Variable.Set(new global::app.data.@this("goal", goal, context: context));

        // The compile result's actions, exactly as they ride: a clr(json) array.
        const string actionsJson = "[{\"module\":\"output\",\"action\":\"write\",\"parameters\":[]}]";
        var acts = await context.Ok(actionsJson, "json");
        acts.Name = "acts";
        await context.Variable.Set(acts);

        var set = TestAction.Create("variable", "set",
            ("name", "%goal.Steps[0].Actions%"), ("value", "%acts%"));
        var result = await set.RunAsync(context);

        await result.IsSuccess();
        await Assert.That(goal.Steps[0].Actions.Count).IsEqualTo(1);
        await Assert.That(goal.Steps[0].Actions[0].Module).IsEqualTo("output");
    }
}
