using When = global::app.@event.When;
using Scope = global::app.@event.binding.Scope;

namespace PLang.Tests.App.Events;

// A goal loads through the goal type's on.load when goal.list reads its .pr: before it, handed the .pr (there is no
// goal yet); after it, the goal. A failing or cancelling before-load is the load's answer and nothing is read. A goal
// already held isn't loaded again.
public class LoadTests
{
    private string _root = null!;

    // An app root holding one current .pr, /.build/fullpipeline.pr, written the way the builder writes one.
    [Before(Test)]
    public async Task Setup()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-load-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_root, ".build"));
        await using var writer = TestApp.Create(_root);
        var goal = Make.Goal("FullPipeline",
            Make.Step("set x", Make.Action("variable", "set", Make.Param("Name", "x", "variable"), ("Value", 1))));
        var serializer = (global::app.channel.serializer.plang.@this)
            writer.User.Channel.Serializers.GetOrDefault("application/plang");
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(_root, ".build", "fullpipeline.pr"), await serializer.Text(goal));
    }

    [After(Test)]
    public void Cleanup() => System.IO.Directory.Delete(_root, recursive: true);

    private string Fixtures() => _root;

    private const string Pr = "/.build/fullpipeline.pr";

    [Test] public async Task BeforeLoad_IsHandedThePr_AfterLoad_TheGoal()
    {
        await using var app = TestApp.Create(Fixtures());
        var handed = new List<global::app.type.item.@this>();
        var on = app.goal.Own();
        on.Bind("load", When.before, (item, _, ctx) => { handed.Add(item); return Task.FromResult(ctx.Ok()); }, app.System, Scope.app);
        on.Bind("load", When.after, (item, _, ctx) => { handed.Add(item); return Task.FromResult(ctx.Ok()); }, app.System, Scope.app);

        var loaded = await app.goal.list.Load(Pr);

        await loaded.IsSuccess();
        await Assert.That(handed.Count).IsEqualTo(2);
        await Assert.That(handed[0] is global::app.type.item.path.@this).IsTrue();
        await Assert.That(handed[0].ToString()).EndsWith("fullpipeline.pr");
        await Assert.That(ReferenceEquals(handed[1], await loaded.Value())).IsTrue();
    }

    [Test] public async Task AFailingBeforeLoad_IsTheAnswer_AndNothingIsRead()
    {
        await using var app = TestApp.Create(Fixtures());
        app.goal.Own().Bind("load", When.before,
            (_, _, ctx) => Task.FromResult(ctx.Error(new global::app.error.Error("no", "Refused", 400))), app.System, Scope.app);

        var loaded = await app.goal.list.Load(Pr);

        await loaded.IsFailure();
        await Assert.That(loaded.Error!.Key).IsEqualTo("Refused");
        await Assert.That(await app.goal.list.Find("FullPipeline")).IsNull();
    }

    [Test] public async Task ACancellingBeforeLoad_IsTheAnswer_AndNothingIsRead()
    {
        await using var app = TestApp.Create(Fixtures());
        app.goal.Own().Bind("load", When.before, (_, _, ctx) =>
        {
            var instead = ctx.Ok("instead");
            instead.Handled = true;
            return Task.FromResult(instead);
        }, app.System, Scope.app);

        var loaded = await app.goal.list.Load(Pr);

        await Assert.That((await loaded.Value())?.ToString()).IsEqualTo("instead");
        await Assert.That(app.goal.list.Count.ToInt32()).IsEqualTo(0);
    }

    [Test] public async Task AFailingAfterLoad_IsTheAnswer()
    {
        await using var app = TestApp.Create(Fixtures());
        app.goal.Own().Bind("load", When.after,
            (_, _, ctx) => Task.FromResult(ctx.Error(new global::app.error.Error("no", "Broke", 400))), app.System, Scope.app);

        var loaded = await app.goal.list.Load(Pr);

        await loaded.IsFailure();
        await Assert.That(loaded.Error!.Key).IsEqualTo("Broke");
    }

    [Test] public async Task AHeldGoal_IsNotLoadedAgain()
    {
        await using var app = TestApp.Create(Fixtures());
        var loads = 0;
        app.goal.Own().Bind("load", When.after, (_, _, ctx) => { loads++; return Task.FromResult(ctx.Ok()); }, app.System, Scope.app);

        await (await app.goal.list.Load(Pr)).IsSuccess();
        await (await app.goal.list.Load(Pr)).IsSuccess();

        await Assert.That(loads).IsEqualTo(1);
    }
}
