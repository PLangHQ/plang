namespace PLang.Tests.App.Modules.TestModuleTests;

/// <summary>
/// A test's tags are its goal's, stamped when the goal's steps are built: a <c>tag this test 'skip'</c> step puts
/// 'skip' on the goal, its .pr carries it, and the test born from that goal is Skipped.
/// </summary>
public class TagStampTests
{
    [Test]
    public async Task AGoalTaggedSkip_CarriesTheTagInItsPr_AndIsSkipped()
    {
        var root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_tagstamp_" + Guid.NewGuid().ToString("N")[..8])).FullName;
        await using var app = global::PLang.Tests.Session.App(root, autoWireConsoleChannels: false).Testing().Building();
        var context = app.actor.list.User.Context;
        var goal = global::app.goal.@this.Parse("Start\n- test.tag(Tags=[\"skip\"])\n",
            global::app.type.item.path.@this.Resolve("/Start.test.goal", context), context)!;

        var refused = await goal.Step.Read("", context);

        await Assert.That(refused?.Message).IsNull();
        await Assert.That(goal.Tag.Has(new global::app.type.item.tag.@this("skip", app.type.list[typeof(global::app.type.item.tag.@this)]))).IsTrue();
        await Assert.That(await context.Pr(goal)).Contains("\"tag\"");
        var test = await global::app.test.@this.From(goal, context);
        await Assert.That(test.Status).IsEqualTo(global::app.test.Status.Skipped);
    }
}
