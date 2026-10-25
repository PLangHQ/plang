namespace PLang.Tests.App.Serialization;

// An empty list says nothing, as a null says nothing: a goal with no sub-goals writes no "child", and reads back with none.
public class PrEmptyListTests
{
    private static string Root => System.IO.Path.Combine(Fixture.Root(), "test");

    private static async Task<global::app.goal.@this> Read(global::app.@this app, string text)
        => await global::PLang.Tests.Shared.RealGoalLoad.Read(app, text,
            global::app.type.item.path.@this.Resolve("/.build/setdeciderkey.pr", app.actor.list.User.Context));

    [Test]
    public async Task AGoalWithNoSubGoals_WritesNoChild()
    {
        await using var app = new global::app.@this(Root, global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var goal = await Read(app, System.IO.File.ReadAllText(System.IO.Path.Combine(Root, ".build", "setdeciderkey.pr")));

        var written = await context.Pr(goal);

        await Assert.That(goal.Child.CountRaw).IsEqualTo(0);
        await Assert.That(written).DoesNotContain("\"child\"");
        await Assert.That(written).Contains("\"step\"");
        await Assert.That((await Read(app, written)).Child.CountRaw).IsEqualTo(0);
    }
}
