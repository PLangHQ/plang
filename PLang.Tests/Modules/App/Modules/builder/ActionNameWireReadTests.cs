using app;
using Type = global::app.type.@this;

namespace PLang.Tests.App.Modules.builder;

// The .pr's own keys populate an action read from a clr(json) host onto a list<action> slot —
// the builder's `set %goal.step[i].action% = %properties.step[i].action%` graft. The LLM answers
// in those keys; no alias is read.
public class ActionNameWireReadTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create(
        "/tmp/actionname-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private async System.Threading.Tasks.Task<Goal> ReadOneAction(string actionsJson)
    {
        var context = _app.User.Context;
        // Goal first, then its step — a step is born knowing its goal (Goal is init).
        var goal = new Goal
        {
            Name = "G",
            Path = global::app.type.item.path.@this.Resolve("/G.goal", context),
            PrPath = global::app.type.item.path.@this.Resolve("/G.pr", context),
        };
        goal.Step.Add(new Step { Goal = goal, Index = 0, Text = "do stuff" });
        _app.Goal.Add(goal);
        await context.Variable.Set("goal", goal);

        var element = System.Text.Json.JsonDocument.Parse(actionsJson).RootElement.Clone();
        var clrJsonActions = new global::app.data.@this("actions",
            Type.Create("object", "json", context: context).Create(element, context), context: context);
        await context.Variable.Set("goal.Step[0].Action", clrJsonActions);
        return goal;
    }

    [Test]
    public async Task WireKey_name_PopulatesActionName()
    {
        var goal = await ReadOneAction("""[ { "module": "output", "name": "write" } ]""");
        await Assert.That(goal.Step[0].Action.Count).IsEqualTo(1);
        await Assert.That(goal.Step[0].Action[0].Module.Name).IsEqualTo("output");
        await Assert.That(goal.Step[0].Action[0].Name).IsEqualTo("write");
    }

    // A row declared `action` holds program: the holding action's reader reads it, so the held action
    // is born holding the same step (the registry could not mint one).
    [Test]
    public async Task ActionTypedRow_ReadsAsHeldAction_BornWithTheStep()
    {
        var goal = await ReadOneAction("""
        [ { "module": "event", "name": "on",
            "parameter": [
              { "name": "Trigger", "type": { "name": "text" }, "value": "BeforeGoal" },
              { "name": "Goal", "type": { "name": "action" },
                "value": { "module": "goal", "name": "call",
                           "parameter": [ { "name": "Name", "type": { "name": "text" }, "value": "LogIt" } ] } } ] } ]
        """);
        var on = goal.Step[0].Action[0];
        var held = on.Parameter.First(p => p.Name == "Goal").Peek() as global::app.goal.step.action.@this;
        await Assert.That(held).IsNotNull();
        await Assert.That(held!.Module.Name).IsEqualTo("goal");
        await Assert.That(held.Name).IsEqualTo("call");
        await Assert.That(held.Step).IsSameReferenceAs(goal.Step[0]);
        await Assert.That((await held.Parameter.First(p => p.Name == "Name").Value())?.RawText).IsEqualTo("LogIt");
    }

    [Test]
    public async Task WireKey_parameter_PopulatesParameter()
    {
        var goal = await ReadOneAction("""
        [ { "module": "output", "name": "write",
            "parameter": [ { "name": "Data", "type": { "name": "text" }, "value": "hi" } ] } ]
        """);
        await Assert.That(goal.Step[0].Action[0].Parameter.Count).IsEqualTo(1);
        await Assert.That(goal.Step[0].Action[0].Parameter[0].Name).IsEqualTo("Data");
    }
}
