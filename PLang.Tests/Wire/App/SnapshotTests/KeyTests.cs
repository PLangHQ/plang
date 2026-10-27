namespace PLang.Tests.App.SnapshotTests;

// In a snapshot, a step and an action are written as what finds them: a step by its goal and its index, an action by
// its step, its index and what it is (module and name).
public class KeyTests
{
    private static async Task<(Goal, Step, global::app.goal.step.action.@this, global::app.actor.context.@this)> Holds(global::app.@this app)
    {
        var context = app.actor.list.User.Context;
        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", context) };
        var step = new Step(app.type.list) { Goal = goal, List = goal.Step, Index = 0, Text = "set %x% = 1" };
        var action = (await context.Action("variable.set(Name=%x%, Value=1)")).In(step);
        step.Code.Add(action);
        goal.Step.Add(step);
        return (goal, step, action, context);
    }

    private static async Task<System.Text.Json.Nodes.JsonNode> Written(global::app.type.item.@this value, global::app.actor.context.@this context)
        => System.Text.Json.Nodes.JsonNode.Parse((string)(await global::app.type.item.wire.@this.Of(value, global::app.View.Snapshot, context)).Raw)!;

    [Test]
    public async Task AStep_IsWrittenAsItsGoalAndItsIndex()
    {
        await using var app = new global::app.@this(System.IO.Path.GetTempPath(), global::PLang.Tests.Parent.Actor).Testing();
        var (goal, step, _, context) = await Holds(app);

        var json = await Written(step, context);

        await Assert.That(json["index"]!.GetValue<int>()).IsEqualTo(0);
        await Assert.That(json["goal"]!["name"]!.GetValue<string>()).IsEqualTo("G");
        await Assert.That(json["goal"]!["hash"]!.GetValue<string>()).IsEqualTo(goal.Hash);
        await Assert.That(json.ToJsonString()).DoesNotContain("set %x% = 1");
    }

    [Test]
    public async Task AnAction_IsWrittenAsItsStepItsIndexAndWhatItIs()
    {
        await using var app = new global::app.@this(System.IO.Path.GetTempPath(), global::PLang.Tests.Parent.Actor).Testing();
        var (_, _, action, context) = await Holds(app);

        var json = await Written(action, context);

        await Assert.That(json["index"]!.GetValue<int>()).IsEqualTo(0);
        await Assert.That(json["module"]!["name"]!.GetValue<string>()).IsEqualTo("variable");
        await Assert.That(json["action"]!.GetValue<string>()).IsEqualTo("set");
        await Assert.That(json["step"]!["index"]!.GetValue<int>()).IsEqualTo(0);
        await Assert.That(json["step"]!["goal"]!["name"]!.GetValue<string>()).IsEqualTo("G");
    }
}
