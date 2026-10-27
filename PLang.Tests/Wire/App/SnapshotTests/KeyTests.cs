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
        await Assert.That(json["name"]!.GetValue<string>()).IsEqualTo("set");
        await Assert.That(json["step"]!["index"]!.GetValue<int>()).IsEqualTo(0);
        await Assert.That(json["step"]!["goal"]!["name"]!.GetValue<string>()).IsEqualTo("G");
    }

    // What its key was written as, read back in the snapshot view as <typeparamref name="T"/>, the type a member holding
    // it declares (a frame's Action).
    private static async Task<global::app.data.@this> Read<T>(T value, global::app.actor.context.@this context) where T : global::app.type.item.@this
    {
        var written = await global::app.type.item.wire.@this.Of(value, global::app.View.Snapshot, context);
        var wire = new global::app.type.item.wire.@this((string)written.Raw, context.App.type.list[typeof(T)],
            (global::app.type.item.wire.kind.plang.@this)context.App.type.list["wire"].kind["plang"]!, global::app.View.Snapshot);
        var read = new global::app.data.@this("", wire, context: context);
        await read.Value();
        return read;
    }

    [Test]
    public async Task AStep_IsReadBackAsTheLiveStep_ThroughItsGoal()
    {
        await using var app = new global::app.@this(System.IO.Path.GetTempPath(), global::PLang.Tests.Parent.Actor).Testing();
        var (goal, step, _, context) = await Holds(app);
        app.goal.list.Add(goal);

        var read = await Read(step, context);

        await Assert.That(await read.Value()).IsSameReferenceAs(step);
    }

    [Test]
    public async Task AnAction_IsReadBackAsTheLiveAction_ThroughItsStep()
    {
        await using var app = new global::app.@this(System.IO.Path.GetTempPath(), global::PLang.Tests.Parent.Actor).Testing();
        var (goal, _, action, context) = await Holds(app);
        app.goal.list.Add(goal);

        var read = await Read(action, context);

        await Assert.That(await read.Value()).IsSameReferenceAs(action);
    }

    [Test]
    public async Task AnActionChangedAtItsPlace_IsRefused()
    {
        await using var app = new global::app.@this(System.IO.Path.GetTempPath(), global::PLang.Tests.Parent.Actor).Testing();
        var (goal, _, action, context) = await Holds(app);
        app.goal.list.Add(goal);
        var written = await global::app.type.item.wire.@this.Of(action, global::app.View.Snapshot, context);
        // the same step text, built into another action since the save
        action.Name = "get";
        var wire = new global::app.type.item.wire.@this((string)written.Raw, context.App.type.list[typeof(global::app.goal.step.action.@this)],
            (global::app.type.item.wire.kind.plang.@this)context.App.type.list["wire"].kind["plang"]!, global::app.View.Snapshot);
        var other = new global::app.data.@this("", wire, context: context);

        await other.Value();

        await Assert.That(other.Error?.Key).IsEqualTo("CallbackActionMismatch");
    }
}
