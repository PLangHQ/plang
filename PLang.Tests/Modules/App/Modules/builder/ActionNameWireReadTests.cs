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
            context.App.Type[new Type("object", "json")].Create(element, context), context: context);
        await context.Variable.Set("goal.Step[0].Code", clrJsonActions);
        return goal;
    }

    [Test]
    public async Task WireKey_name_PopulatesActionName()
    {
        var goal = await ReadOneAction("""[ { "module": "output", "name": "write" } ]""");
        await Assert.That(goal.Step[0].Code.Count).IsEqualTo(1);
        await Assert.That(goal.Step[0].Code[0].Module.Name).IsEqualTo("output");
        await Assert.That(goal.Step[0].Code[0].Name).IsEqualTo("write");
    }

    // A row declared `action` holds program: the holding action's reader reads it, so the held action
    // is born holding the same step (the registry could not mint one).
    [Test]
    public async Task ActionTypedRow_ReadsAsHeldAction_BornWithTheStep()
    {
        var goal = await ReadOneAction("""
        [ { "module": "event", "name": "on",
            "property": [
              { "name": "Trigger", "type": { "name": "text" }, "value": "BeforeGoal" },
              { "name": "Goal", "type": { "name": "action" },
                "value": { "module": "goal", "name": "call",
                           "property": [ { "name": "Name", "type": { "name": "text" }, "value": "LogIt" } ] } } ] } ]
        """);
        var on = goal.Step[0].Code[0];
        var held = on["Goal"]!.Value as global::app.goal.step.action.@this;
        await Assert.That(held).IsNotNull();
        await Assert.That(held!.Module.Name).IsEqualTo("goal");
        await Assert.That(held.Name).IsEqualTo("call");
        await Assert.That(held.Step).IsSameReferenceAs(goal.Step[0]);
        // A reader of the program makes its own Data, with its own context.
        await Assert.That((await held["Name"]!.Data(_app.User.Context).Value())?.RawText).IsEqualTo("LogIt");
    }

    [Test]
    public async Task WireKey_parameter_PopulatesParameter()
    {
        var goal = await ReadOneAction("""
        [ { "module": "output", "name": "write",
            "property": [ { "name": "Data", "type": { "name": "text" }, "value": "hi" } ] } ]
        """);
        await Assert.That(goal.Step[0].Code[0].Property.Count).IsEqualTo(1);
        await Assert.That(goal.Step[0].Code[0].Property[0].Name).IsEqualTo("Data");
    }
}
