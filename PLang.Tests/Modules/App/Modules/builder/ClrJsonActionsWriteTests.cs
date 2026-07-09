using app;
using Type = global::app.type.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Pins the builder blocker (navigation-driven-record-builder, Stage 1): writing a
/// clr(json) actions array onto a plang-typed <c>%goal.Steps[i].Actions%</c> slot.
///
/// The builder's compile step does <c>set %goal.Steps[i].Actions% = %compileResult.actions%</c>,
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
            Path = global::app.type.path.@this.Resolve("/G.goal", context),
            PrPath = global::app.type.path.@this.Resolve("/G.pr", context),
            Steps = new GoalSteps { new Step { Index = 0, Text = "do stuff" } },
        };
        goal.Steps[0].Goal = goal;
        _app.Goal.Add(goal);
        await context.Variable.Set("goal", goal);

        // The compile result: a clr(json) array of action objects — exactly what the LLM
        // compile hands back (`%compileResult.actions%`).
        // Params ride as full Data wire objects {name, type, value} — the shape the
        // @schema:data reader materializes (a bare {name,value} has no declared type).
        const string actionsJson = """
        [
          { "module": "variable", "action": "set",
            "parameters": [
              { "name": "Name",  "type": { "name": "text" }, "value": "x" },
              { "name": "Value", "type": { "name": "text" }, "value": "1" } ] },
          { "module": "output", "action": "write",
            "parameters": [
              { "name": "content", "type": { "name": "text" }, "value": "%x%" } ] }
        ]
        """;
        // A genuine clr(json): JsonElement-backed, as the reader produces for json-kind
        // content (the json kind rides on JsonElement, not JsonNode).
        var element = System.Text.Json.JsonDocument.Parse(actionsJson).RootElement.Clone();
        var clrJsonActions = global::app.data.@this.FromRaw(
            element, Type.Create("object", "json", context: context), context, "actions");

        // The builder write. Today this throws in ClrConvert; after Stage 1 it builds
        // two action hosts onto the slot.
        await context.Variable.Set("goal.Steps[0].Actions", clrJsonActions);

        await Assert.That(goal.Steps[0].Actions.Count).IsEqualTo(2);
        await Assert.That(goal.Steps[0].Actions[0].Module).IsEqualTo("variable");
        await Assert.That(goal.Steps[0].Actions[0].ActionName).IsEqualTo("set");
        await Assert.That(goal.Steps[0].Actions[1].Module).IsEqualTo("output");
        await Assert.That(goal.Steps[0].Actions[1].ActionName).IsEqualTo("write");
    }
}
