using app;
using Type = global::app.type.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Pins the builder blocker (navigation-driven-record-builder, Stage 1): writing a
/// clr(json) actions array onto a plang-typed <c>%goal.Step[i].Code%</c> slot.
///
/// The builder's compile step does <c>set %goal.Step[i].Code% = %compileResult.actions%</c>,
/// where the RHS is a clr(json) (the LLM result). Today the write lowers the clr(json)
/// into <c>StepActions</c>/<c>List&lt;action&gt;</c> via <c>ClrConvert</c>, which terminal-throws
/// ("the type must own this Clr projection") — the built goal ends up with no actions.
///
/// RED now (throws), GREEN after Stage 1 (the json kind's <c>Kind.Clr</c> builds the
/// action hosts). This test does not chase any other failing suite — it is the single
/// Stage-1 acceptance anchor.
/// </summary>
public class ClrJsonActionsWriteTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create(
        "/tmp/clrjson-actions-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task ClrJsonActionsArray_WritesOntoStepActionsSlot_AsActionHosts()
    {
        var context = _app.User.Context;

        // A goal with one step whose Actions collection is empty (as right before the
        // builder writes the compiled actions in).
        var goal = new Goal
        {
            Name = "G",
            Path = global::app.type.item.path.@this.Resolve("/G.goal", context),
            PrPath = global::app.type.item.path.@this.Resolve("/G.pr", context),
        };
        goal.Step.Add(new Step { Goal = goal, Index = 0, Text = "do stuff" });
        _app.Goal.Add(goal);
        // goal flows as clr<goal> now (a host); the builder holds it that way, so %goal%
        // navigates/writes through the clr carrier's reflection kind.
        await context.Variable.Set("goal", goal);

        // The compile result: a clr(json) array of action objects — exactly what the LLM
        // compile hands back (`%compileResult.actions%`).
        // Params ride as full Data wire objects {name, type, value} — the shape the
        // @schema:data reader materializes (a bare {name,value} has no declared type).
        const string actionsJson = """
        [
          { "module": "variable", "name": "set",
            "property": [
              { "name": "Name",  "type": { "name": "text" }, "value": "x" },
              { "name": "Value", "type": { "name": "text" }, "value": "1" } ] },
          { "module": "output", "name": "write",
            "property": [
              { "name": "content", "type": { "name": "text" }, "value": "%x%" } ] }
        ]
        """;
        // A genuine clr(json): JsonElement-backed, as the reader produces for json-kind
        // content (the json kind rides on JsonElement, not JsonNode).
        var element = System.Text.Json.JsonDocument.Parse(actionsJson).RootElement.Clone();
        var clrJsonActions = new global::app.data.@this("actions",
            context.App.Type[new Type("object", "json")].Create(element, context), context: context);

        // The builder write: the STEP constructs its children from the incoming json.
        await context.Variable.Set("goal.Step[0].Code", clrJsonActions);

        await Assert.That(goal.Step[0].Code.Count).IsEqualTo(2);
        await Assert.That(goal.Step[0].Code[0].Module.Name).IsEqualTo("variable");
        await Assert.That(goal.Step[0].Code[0].Name).IsEqualTo("set");
        await Assert.That(goal.Step[0].Code[1].Module.Name).IsEqualTo("output");
        await Assert.That(goal.Step[0].Code[1].Name).IsEqualTo("write");

        // The birth fact: an action written onto a step belongs to that step from the moment it is
        // written. The host constructs its children — nothing stamps a step on afterwards.
        await Assert.That(goal.Step[0].Code[0].Step).IsEqualTo(goal.Step[0]);
        await Assert.That(goal.Step[0].Code[1].Step).IsEqualTo(goal.Step[0]);
    }
}
