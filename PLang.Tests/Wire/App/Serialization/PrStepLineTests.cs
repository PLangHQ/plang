namespace PLang.Tests.App.Serialization;

// A step's line is written like the rest of the step: by its tags, its indent always.
public class PrStepLineTests
{
    private static string Root => System.IO.Path.Combine(Fixture.Root(), "test");

    [Test]
    public async Task AnUnindentedStep_WritesItsIndent()
    {
        await using var app = new global::app.@this(Root).Testing();
        var context = app.actor.list.User.Context;
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.Read(app,
            System.IO.File.ReadAllText(System.IO.Path.Combine(Root, ".build", "setdeciderkey.pr")),
            global::app.type.item.path.@this.Resolve("/.build/setdeciderkey.pr", context));

        var written = await context.Pr(goal);

        await Assert.That(goal.Step[0].Line.Indent).IsEqualTo(0);
        await Assert.That(written).Contains("\"line\": {\n        \"number\": 2,\n        \"indent\": 0\n      }");
    }
}
