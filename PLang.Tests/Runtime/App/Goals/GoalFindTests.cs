namespace PLang.Tests.App.Goals;

// A call names one goal: an address (/lib/CheckPolicy) by its exact path, never another that only ends the same way;
// a bare name (Log) the one beside its caller, before any other of that name already read.
public class GoalFindTests
{
    private static Goal At(global::app.@this app, string path)
    {
        var context = app.actor.list.User.Context;
        var goal = new Goal(app.type.list) { Name = System.IO.Path.GetFileNameWithoutExtension(path), Path = global::app.type.item.path.@this.Resolve(path, context) };
        var step = new Step(app.type.list) { Goal = goal, List = goal.Step, Index = 0, Text = $"{path} step 0" };
        step.Code.Add(Make.Action(context, "variable", "set", Make.Param(context, "Name", "x", "variable"), ("Value", "1")).In(step));
        goal.Step.Add(step);
        app.goal.list.Add(goal);
        return goal;
    }

    [Test]
    public async Task AnAddress_FindsItsExactPath_NeverAGoalThatOnlyEndsTheSameWay()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var plugin = At(app, "/plugin/lib/CheckPolicy.goal");

        var found = await app.goal.list.Find("/lib/CheckPolicy");

        await Assert.That(found.Success && ReferenceEquals(await found.Value(), plugin)).IsFalse();
    }

    [Test]
    public async Task ABareName_FindsTheGoalBesideItsCaller_BeforeAnotherReadLater()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var mine = At(app, "/a/Log.goal");
        At(app, "/b/Log.goal");
        var caller = At(app, "/a/G.goal");

        var found = await app.goal.list.Find("Log", caller);

        await found.IsSuccess();
        await Assert.That(await found.Value()).IsSameReferenceAs(mine);
    }
}
