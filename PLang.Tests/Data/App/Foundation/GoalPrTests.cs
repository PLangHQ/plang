using app;
using app.Utils;

namespace PLang.Tests.App.Foundation;

/// <summary>
/// A goal's .pr is derived from its Path, never set: a file reference at the .pr its .goal is built to.
/// </summary>
public class GoalPrTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task Pr_DerivedFromPath_Correctly()
    {
        var goal = new Goal(app.type.list) { Name = "Test", Path = global::app.type.item.path.@this.Resolve("\\Test.goal".AdjustPathToOs(), app.actor.list.User.Context) };
        await Assert.That(goal.Pr?.Path.ToString()).IsEqualTo("/.build/test.pr");
    }

    [Test]
    public async Task Pr_SubDirectory_DerivedCorrectly()
    {
        var goal = new Goal(app.type.list) { Name = "Inner", Path = global::app.type.item.path.@this.Resolve("\\SubDir\\Inner.goal".AdjustPathToOs(), app.actor.list.User.Context) };
        await Assert.That(goal.Pr?.Path.ToString()).IsEqualTo("/SubDir/.build/inner.pr");
    }

    [Test]
    public async Task Pr_ForwardSlashPath_DerivedCorrectly()
    {
        var goal = new Goal(app.type.list) { Name = "Test", Path = global::app.type.item.path.@this.Resolve("/Test.goal", app.actor.list.User.Context) };
        await Assert.That(goal.Pr?.Path.ToString()).IsEqualTo("/.build/test.pr");
    }

    [Test]
    public async Task Pr_NullPath_ReturnsNull()
    {
        var goal = new Goal(app.type.list) { Name = "Test" };
        await Assert.That(goal.Pr?.Path).IsNull();
    }

    [Test]
    public async Task Pr_UpdatesWhenPathChanges()
    {
        var goal = new Goal(app.type.list) { Name = "Test", Path = global::app.type.item.path.@this.Resolve("\\Test.goal".AdjustPathToOs(), app.actor.list.User.Context) };
        await Assert.That(goal.Pr?.Path.ToString()).IsEqualTo("/.build/test.pr");

        goal.Path = global::app.type.item.path.@this.Resolve("\\Other\\Test.goal".AdjustPathToOs(), app.actor.list.User.Context);
        await Assert.That(goal.Pr?.Path.ToString()).IsEqualTo("/Other/.build/test.pr");
    }
}
