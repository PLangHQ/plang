using When = global::app.@event.When;
using Scope = global::app.@event.binding.Scope;

namespace PLang.Tests.App.Events;

// The app is an item: %!app% navigates as it did, a copy of it is it, and it starts through its on.start around the
// entry goal — what is bound before it answers first (a refusal or a cancel is the answer and the goal doesn't run),
// every after runs on the result. An app start binding is the app's: one bound by any actor fires.
public class AppStartTests
{
    private string _root = null!;

    // An app root holding one current .pr, /.build/entry.pr — a goal that sets %x% — written the way the builder does.
    [Before(Test)]
    public async Task Setup()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-appstart-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_root, ".build"));
        await using var writer = TestApp.Create(_root);
        var goal = Make.Goal("Entry",
            Make.Step("set x", Make.Action("variable", "set", Make.Param("Name", "x", "variable"), ("Value", 1))));
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(_root, ".build", "entry.pr"), await writer.User.Context.Pr(goal));
    }

    [After(Test)]
    public void Cleanup() => System.IO.Directory.Delete(_root, recursive: true);

    private async Task<global::app.@this> App()
    {
        var app = TestApp.Create(_root);
        await app.System.Context.Variable.Set("goalFile", "/.build/entry.pr");
        return app;
    }

    private static async Task<global::app.data.@this> Read(string variable, global::app.actor.context.@this context)
        => await new global::app.type.item.variable.parser.@this(variable).Variable.Single().Start(context);

    [Test] public async Task TheApp_IsAnItem_AndNavigatesAsItDid()
    {
        await using var app = TestApp.Create("/test");
        var context = app.User.Context;

        await Assert.That(ReferenceEquals((await Read("%!app%", context)).Peek(), app)).IsTrue();
        await Assert.That(ReferenceEquals((await Read("%!app.variable%", context)).Peek(), app.variable)).IsTrue();
        await Assert.That((await Read("%!app.goal%", context)).IsInitialized).IsTrue();
        await Assert.That(app.Type.Name).IsEqualTo("app");
    }

    [Test] public async Task ACopyOfTheApp_IsTheApp()
    {
        await using var app = TestApp.Create("/test");
        await Assert.That(ReferenceEquals(app.Clone(), app)).IsTrue();
    }

    [Test] public async Task TheApp_StartsThroughItsOnStart_AroundTheEntryGoal_AppScoped()
    {
        await using var app = await App();
        var ran = new List<string>();
        var own = app.Own();
        // bound by the System actor, for the app: it fires though the goal runs as the User
        own.Bind("start", When.before, (item, _, ctx) => { lock (ran) ran.Add($"before {(item as global::app.@this)?.Name}"); return Task.FromResult(ctx.Ok()); },
            app.System, Scope.app);
        own.Bind("start", When.after, async (_, result, ctx) =>
        {
            var x = (await ctx.Variable.Get("x")).IsInitialized;
            lock (ran) ran.Add($"after x={x}");
            return ctx.Ok();
        }, app.System, Scope.app);

        var result = await app.Start();

        await result.IsSuccess();
        await Assert.That(ran).IsEquivalentTo(new[] { $"before {app.Name}", "after x=True" },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test] public async Task ARefusingBefore_IsTheAnswer_AndTheGoalDoesntRun()
    {
        await using var app = await App();
        app.Own().Bind("start", When.before,
            (_, _, ctx) => Task.FromResult(ctx.Error(new global::app.error.Error("no", "Refused", 400))), app.User, Scope.app);

        var result = await app.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Refused");
        await Assert.That((await app.User.Context.Variable.Get("x")).IsInitialized).IsFalse();
    }

    [Test] public async Task ACancellingBefore_IsTheAnswer_AndTheGoalDoesntRun()
    {
        await using var app = await App();
        app.Own().Bind("start", When.before, (_, _, ctx) =>
        {
            var instead = ctx.Ok("instead");
            instead.Handled = true;
            return Task.FromResult(instead);
        }, app.User, Scope.app);

        var result = await app.Start();

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("instead");
        await Assert.That((await app.User.Context.Variable.Get("x")).IsInitialized).IsFalse();
    }
}
