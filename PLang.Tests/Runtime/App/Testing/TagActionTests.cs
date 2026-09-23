using Tag = global::app.module.action.test.Tag;

namespace PLang.Tests.App.Tester;

/// <summary>
/// test.tag — "- tag this test 'http', 'fast'". The tags are the goal's own build-birth fact: Build()
/// stamps them on the goal being built (%goal%), the goal writes them into its .pr and reads them
/// back. Running the step does nothing.
/// </summary>
public class TagActionTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/test");
    }

    [After(Test)]
    public async Task Teardown() => await _app.DisposeAsync();

    private global::app.goal.@this Goal() =>
        global::PLang.Tests.Shared.Make.Goal("Start", "/Tests/T.test.goal",
            global::PLang.Tests.Shared.Make.Step("set %x% = 1",
                global::PLang.Tests.Shared.Make.Action("variable", "set", new (string, object?)[] { ("Name", "x"), ("Value", 1) })));

    private global::app.data.@this<global::app.type.item.list.@this> Tags(params string[] tags) =>
        new("Tags", global::PLang.Tests.Shared.Make.List(tags, Ctx));

    private static global::app.type.item.tag.@this T(string value) => new(value);

    [Test]
    public async Task Build_StampsTheGoalBeingBuilt()
    {
        var goal = Goal();
        await Ctx.Variable.Set("goal", goal);

        var result = await new Tag(Ctx) { Tags = Tags("http", "Fast") }.Build();

        await result.IsSuccess();
        await Assert.That(goal.Tag.Has(T("http"))).IsTrue();
        await Assert.That(goal.Tag.Has(T("fast"))).IsTrue();
    }

    [Test]
    public async Task Build_SameTagTwice_StampedOnce()
    {
        var goal = Goal();
        await Ctx.Variable.Set("goal", goal);

        await new Tag(Ctx) { Tags = Tags("http") }.Build();
        await new Tag(Ctx) { Tags = Tags("HTTP", "slow") }.Build();

        await Assert.That(goal.Tag.CountRaw).IsEqualTo(2);
    }

    [Test]
    public async Task Build_VariableTags_StampNothing()
    {
        var goal = Goal();
        await Ctx.Variable.Set("goal", goal);

        var action = global::PLang.Tests.Shared.Make.Action("test", "tag", new (string, object?)[] { ("Tags", "%myTags%") });
        await Assert.That(await action.Build(Ctx)).IsNull();

        await Assert.That(goal.Tag.CountRaw).IsEqualTo(0);
    }

    [Test]
    public async Task Run_IsPlainOk_NoValue()
    {
        var result = await new Tag(Ctx) { Tags = Tags("http") }.Run();

        await result.IsSuccess();
        await Assert.That(result.HasValue).IsFalse();
    }

    [Test]
    public async Task GoalTag_RoundTripsThroughTheGoalsOwnWire()
    {
        var goal = Goal();
        goal.Tag.Add(T("http"));
        goal.Tag.Add(T("skip"));

        var loaded = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(_app, goal);

        await Assert.That(loaded.Tag.CountRaw).IsEqualTo(2);
        await Assert.That(loaded.Tag.Has(T("http"))).IsTrue();
        await Assert.That(loaded.Tag.Has(T("skip"))).IsTrue();
    }
}
