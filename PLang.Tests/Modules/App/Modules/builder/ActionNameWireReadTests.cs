using app;
using Type = global::app.type.@this;

namespace PLang.Tests.App.Modules.builder;

// Which wire key populates action.Name when reading a clr(json) action host onto a
// list<action> slot — the builder's `set %goal.step[i].action% = %compileResult.actions%` path.
// The LLM returns {"module":..,"action":..}; the proof test used {"module":..,"name":..}.
public class ActionNameWireReadTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create(
        "/tmp/actionname-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private async System.Threading.Tasks.Task<Goal> ReadOneAction(string actionsJson)
    {
        var context = _app.User.Context;
        var goal = new Goal
        {
            Name = "G",
            Path = global::app.type.item.path.@this.Resolve("/G.goal", context),
            PrPath = global::app.type.item.path.@this.Resolve("/G.pr", context),
            Step = new GoalSteps { new Step { Index = 0, Text = "do stuff" } },
        };
        goal.Step[0].Goal = goal;
        _app.Goal.Add(goal);
        await context.Variable.Set("goal", goal);

        var element = System.Text.Json.JsonDocument.Parse(actionsJson).RootElement.Clone();
        var clrJsonActions = new global::app.data.@this("actions",
            Type.Create("object", "json", context: context).Create(element, context), context: context);
        await context.Variable.Set("goal.Step[0].Action", clrJsonActions);
        return goal;
    }

    [Test]
    public async Task WireKey_action_PopulatesActionName()
    {
        var goal = await ReadOneAction("""[ { "module": "output", "action": "write" } ]""");
        await Assert.That(goal.Step[0].Action.Count).IsEqualTo(1);
        await Assert.That(goal.Step[0].Action[0].Module.Name).IsEqualTo("output");
        await Assert.That(goal.Step[0].Action[0].Name).IsEqualTo("write");
    }

    [Test]
    public async Task WireKey_name_PopulatesActionName()
    {
        var goal = await ReadOneAction("""[ { "module": "output", "name": "write" } ]""");
        await Assert.That(goal.Step[0].Action[0].Name).IsEqualTo("write");
    }

    // The LLM pluralizes an array field name (`parameters`) regardless of the schema hint; the
    // reader tolerates it. Canonical wire stays singular (`parameter`).
    [Test]
    public async Task WireKey_parameters_Plural_PopulatesParameter()
    {
        var goal = await ReadOneAction("""
        [ { "module": "output", "action": "write",
            "parameters": [ { "name": "Data", "type": { "name": "text" }, "value": "hi" } ] } ]
        """);
        await Assert.That(goal.Step[0].Action[0].Parameter.Count).IsEqualTo(1);
        await Assert.That(goal.Step[0].Action[0].Parameter[0].Name).IsEqualTo("Data");
    }
}
