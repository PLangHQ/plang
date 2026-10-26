namespace PLang.Tests.App.Types.PathTests;

// A path's plang form — what a goal writes and a .pr holds — is "/" between its parts on every OS: each derivation
// writes it with its own math, never the OS's path functions (on Windows they write "\"). The OS form is the OS's.
public class PlangFormTests
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/plangform-" + System.Guid.NewGuid().ToString("N")[..8], global::PLang.Tests.Parent.Actor).Testing();
    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;
    private global::app.type.item.path.@this File(string typed) => global::app.type.item.path.file.@this.Resolve(typed, Ctx);

    [Test] public async Task Combine_JoinsWithASlash()
        => await Assert.That(File("/data").Combine("a.txt").ToString()).IsEqualTo("/data/a.txt");

    [Test] public async Task Combine_UnderTheGoalFolder_IsTheChild()
        => await Assert.That(File(".").Combine("a.txt").ToString()).IsEqualTo("a.txt");

    // a rooted child is under the path in both forms: they name the same place
    [Test] public async Task Combine_ARootedChild_IsUnderThePath_InBothForms()
    {
        var data = File("/data");
        var child = data.Combine("/x.txt");

        await Assert.That(child.ToString()).IsEqualTo("/data/x.txt");
        await Assert.That(child.Absolute).IsEqualTo(System.IO.Path.Combine(data.Absolute, "x.txt"));
    }

    [Test] public async Task Parent_IsTheFolderBeforeTheLastSlash()
    {
        await Assert.That(File("/data/sub/a.txt").Parent.ToString()).IsEqualTo("/data/sub");
        await Assert.That(File("/a.txt").Parent.ToString()).IsEqualTo("/");
        await Assert.That(File("a.txt").Parent.ToString()).IsEqualTo(".");
    }

    [Test] public async Task WithExtension_ChangesTheLastPartsExtension()
    {
        await Assert.That(File("/data/a.txt").WithExtension(".json").ToString()).IsEqualTo("/data/a.json");
        await Assert.That(File("/data.v1/a").WithExtension("json").ToString()).IsEqualTo("/data.v1/a.json");
    }

    [Test] public async Task AGoalsPr_IsBesideItInDotBuild()
        => await Assert.That(((global::app.type.item.path.file.@this)File("/sub/Inner.goal")).Pr.ToString()).IsEqualTo("/sub/.build/inner.pr");
}
