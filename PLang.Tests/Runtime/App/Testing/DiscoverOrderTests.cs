namespace PLang.Tests.App.Testing;

/// Discovery answers the same tests in the same order whatever the run's width: the listing's order, each file's test
/// (fresh or stale, and why) as one file at a time would make it.
public class DiscoverOrderTests
{
    private static async Task<List<string>> Discovered(int parallel)
    {
        var root = System.IO.Path.Combine(global::PLang.Tests.Shared.Fixture.Root(), "test");
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User.Context;
        await (await user.Setting.Flag("app.test.setting", new Dictionary<string, object?> { ["parallel"] = parallel }, user)).IsSuccess();

        var found = await (await user.Action("test.discover(Path=\"/module\")")).Start(user);
        await found.IsSuccess();
        var tests = (global::app.type.item.list.@this)(await found.Value())!;
        return tests.Items(user).Select(row => (global::app.test.@this)row.Peek()!)
            .Select(test => $"{test.Goal.Path} {test.Status} {test.StatusReason}").ToList();
    }

    [Test]
    public async Task OneAtATime_AndEightAtATime_DiscoverTheSameTestsInTheSameOrder()
    {
        var one = await Discovered(1);
        var eight = await Discovered(8);

        await Assert.That(one.Count).IsGreaterThan(20).Because("a folder of many tests");
        await Assert.That(eight).IsEquivalentTo(one, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
