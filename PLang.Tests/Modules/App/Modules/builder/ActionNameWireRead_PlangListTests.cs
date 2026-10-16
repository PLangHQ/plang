using System.Text.Json;
using app;
using Type = global::app.type.@this;

namespace PLang.Tests.App.Modules.builder;

// Stage 4 (kind-is-type, decision 792): the actions carrier builds as a plang list of dicts (not a
// clr(JsonElement)); the same wire keys populate the step's Code slot — `module`, `name`, `property`
// each read verbatim; a typed `action` row still mints a held action born with the step.
public class ActionNameWireRead_PlangListTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this(
        "/tmp/actionname-list-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    // Parse json into a plang list of dicts (object nodes become Dictionary<string, object?>, arrays become
    // List<object?>, scalars stay their CLR form), then wrap as Data without a type-kind — the kind-aware
    // consumer sees a plang list, not a clr(json).
    private async System.Threading.Tasks.Task<Goal> ReadOneAction(string actionsJson)
    {
        var context = _app.actor.list.User.Context;
        var goal = new Goal
        {
            Name = "G",
            Path = global::app.type.item.path.@this.Resolve("/G.goal", context),
        };
        goal.Step.Add(new Step { Goal = goal, Index = 0, Text = "do stuff" });
        _app.goal.list.Add(goal);
        await context.Variable.Set("goal", goal);

        var plangList = ToPlang(JsonDocument.Parse(actionsJson).RootElement);
        var actions = new global::app.data.@this("actions", plangList, context: context);
        await new global::app.type.item.variable.@this("goal.Step[0].Code").Set(actions, context);
        return goal;
    }

    private static object? ToPlang(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => ToPlang(p.Value)),
        JsonValueKind.Array => e.EnumerateArray().Select(ToPlang).ToList(),
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? (object)l : e.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => null,
    };

    [Test]
    public async Task WireKey_name_PopulatesActionName_FromAPlangList()
    {
        var goal = await ReadOneAction("""[ { "module": "output", "name": "write" } ]""");
        await Assert.That(goal.Step[0].Code.Count).IsEqualTo(1);
        await Assert.That(goal.Step[0].Code[0].Module.Name).IsEqualTo("output");
        await Assert.That(goal.Step[0].Code[0].Name).IsEqualTo("write");
    }

    [Test]
    public async Task ActionTypedRow_ReadsAsHeldAction_BornWithTheStep_FromAPlangList()
    {
        var goal = await ReadOneAction("""
        [ { "module": "on", "name": "event",
            "property": [
              { "name": "Event", "type": { "name": "text" }, "value": "start" },
              { "name": "Action", "type": { "name": "action" },
                "value": { "module": "goal", "name": "call",
                           "property": [ { "name": "Name", "type": { "name": "text" }, "value": "LogIt" } ] } } ] } ]
        """);
        var on = goal.Step[0].Code[0];
        var held = on["Action"]!.Value as global::app.goal.step.action.@this;
        await Assert.That(held).IsNotNull();
        await Assert.That(held!.Module.Name).IsEqualTo("goal");
        await Assert.That(held.Name).IsEqualTo("call");
        await Assert.That(held.Step).IsSameReferenceAs(goal.Step[0]);
    }

    [Test]
    public async Task WireKey_parameter_PopulatesParameter_FromAPlangList()
    {
        var goal = await ReadOneAction("""
        [ { "module": "output", "name": "write",
            "property": [ { "name": "Data", "type": { "name": "text" }, "value": "hi" } ] } ]
        """);
        await Assert.That(goal.Step[0].Code[0].Property.Count).IsEqualTo(1);
        await Assert.That(goal.Step[0].Code[0].Property[0].Name).IsEqualTo("Data");
    }
}
