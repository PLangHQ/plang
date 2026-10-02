using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Shortcut;

/// <summary>
/// A shortcut is a goal under a <c>shortcut/</c> folder, named by its file: <c>%!where%</c> reads as what
/// <c>/shortcut/where.goal</c> answers for its asker. The read writes nothing the asker sees (its <c>%!data%</c>
/// stays), the shortcut answers before the app's member of its name, and <c>%!app.x%</c> still reaches the member.
/// </summary>
public class ShortcutTests
{
    private string _root = null!;
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shortcut-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_root, "shortcut", ".build"));
        _app = new global::app.@this(_root).Testing();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _app.DisposeAsync();
        System.IO.Directory.Delete(_root, recursive: true);
    }

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    // A shortcut goal on disk: <folder>/<name>.goal, its .pr in <folder>/.build/, returning `value`.
    private async Task Shortcut(string name, string value, string folder = "shortcut")
    {
        var goal = Make.Goal(Ctx, name, $"/{folder}/{name}.goal",
            Make.Step($"return {value}", Make.Action(Ctx, "goal", "return", ("Data", value))));
        var at = System.IO.Path.Combine(_root, folder);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(at, ".build"));
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(at, ".build", name.ToLowerInvariant() + ".pr"), await Ctx.Pr(goal));
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(at, name + ".goal"), name + "\n");
    }

    // a reference the build wrote, born as the build births it
    private global::app.data.@this Reference(string variable) => Make.Built(Ctx, "value", variable);

    private global::app.goal.step.action.@this Set(string name, string value)
        => Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", name, "variable"), ("Value", value));

    [Test]
    public async Task AShortcut_ReadsAsItsGoalsAnswer_ForItsAsker()
    {
        await Shortcut("where", "%!app.call.scope.caller.goal%");
        var caller = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(_app,
            Make.Goal(Ctx, "Caller", "/Caller.goal", Make.Step("set %got% = %!where%", Set("got", "%!where%"))));
        _app.goal.list.Add(caller);

        await (await caller.Start(Ctx)).IsSuccess();

        await Assert.That((await Ctx.Variable.Get("got")).Peek()).IsSameReferenceAs(caller);
    }

    [Test]
    public async Task ReadingAShortcut_LeavesTheAskersDataAsItWas()
    {
        await Shortcut("mine", "mine");
        await Ctx.Variable.Set("!data", "before");

        await Assert.That((await Reference("%!mine%").Value())?.ToString()).IsEqualTo("mine");

        await Assert.That((await Ctx.Variable.Get("!data")).GetValue<string>()).IsEqualTo("before");
    }

    [Test]
    public async Task AShortcut_AnswersBeforeTheAppsMember_AndTheAppStillReachesIt()
    {
        await Shortcut("environment", "mine");

        var shortcut = Reference("%!environment%");
        var member = Reference("%!app.environment%");

        await Assert.That((await shortcut.Value())?.ToString()).IsEqualTo("mine");
        await Assert.That((await member.Value())?.ToString()).IsNotEqualTo("mine");
    }

    [Test]
    public async Task TheShortcuts_AreTheAppsList()
    {
        await Shortcut("where", "%!app.call.scope.caller.goal%");

        var read = await _app.shortcut.list.Read();

        await read.IsSuccess();
        await Assert.That(_app.shortcut.list.Items().Select(s => s.Name)).Contains("where");
    }

    // the app's own /system/shortcut/ is the system's: it reads as the shortcut and its name is sealed
    [Test]
    public async Task TheAppsSystemShortcut_ReadsAsTheShortcut_AndIsSealed()
    {
        await Shortcut("here", "mine", folder: "system/shortcut");

        await Assert.That((await Reference("%!here%").Value())?.ToString()).IsEqualTo("mine");
        await Assert.That(_app.shortcut.list.Items().Single(s => s.Name == "here").IsSystem).IsTrue();
    }

    // an app shortcut taking a system name is refused, saying how to override it
    [Test]
    public async Task AnAppShortcut_TakingASystemName_IsRefused_SayingHowToOverride()
    {
        await Shortcut("here", "system", folder: "system/shortcut");
        await Shortcut("here", "app");

        var read = await _app.shortcut.list.Read();

        await read.IsFailure();
        await Assert.That(read.Error!.Key).IsEqualTo("ShortcutCollision");
        await Assert.That(read.Error.Message).Contains("/system/shortcut/here.goal");
    }
}
