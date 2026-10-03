using PLang.Tests.App.Types.PathTests.Contract;

namespace PLang.Tests.App.actions.terminal;

/// <summary>
/// What ships with plang is trusted by its origin (decision 579): a step of a goal under the os folder (<c>/system/</c>)
/// that names the program it starts itself — the program, its arguments, environment and folder written in the step, no
/// %ref% — starts it without asking, as the user's own actor, and nothing is stored. Anything else asks: a user goal; an
/// os goal handed its program by its caller (the confused deputy); a user goal an os goal calls back. Each ask here is
/// answered "n", so a step that asked fails PermissionDenied and one that didn't runs.
/// </summary>
public class OsTrustTests : IDisposable
{
    // outside the app's root and the os folder: starting it is asked unless trusted
    private const string Program = "//bin/true";

    private readonly string _root;
    private readonly global::app.@this _app;

    public OsTrustTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_trust_" + Guid.NewGuid().ToString("N"))).FullName;
        _app = new global::app.@this(_root).Testing();
        Context.Actor!.Channel.Register(new CannedAnswerChannel("n"));
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Directory.Delete(_root, true);
    }

    private global::app.actor.context.@this Context => _app.actor.list.User.Context;

    private global::app.goal.step.action.@this Start(object? app)
        => Make.Action(Context, "terminal", "start", ("App", app));

    private async Task<global::app.goal.@this> Goal(string path, params Make.StepDef[] steps)
    {
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(Context, Path.GetFileNameWithoutExtension(path), path, steps));
        _app.goal.list.Add(goal);
        return goal;
    }

    // a goal of plang's own: in the runtime's os folder. Made in memory, never written there: read off the wire (so
    // its steps are its own, as a loaded .pr's are), then placed in the os folder — a /system/ path is found on disk,
    // and with no file there the read would have put it in the app
    private string Os(string name) => Path.Combine(_app.OsAbsolutePath, "system", "trust", name);

    private async Task<global::app.goal.@this> OsGoal(string name, params Make.StepDef[] steps)
    {
        var goal = await Goal(Os(name), steps);
        goal.Path = global::app.type.item.path.file.@this.Resolve(Os(name), Context);
        await Assert.That(goal.Folder!.Absolute.StartsWith(_app.OsAbsolutePath.TrimEnd('/') + "/", StringComparison.Ordinal))
            .IsTrue().Because("the goal is one of plang's own, under the os folder");
        return goal;
    }

    [Test]
    public async Task AUserGoal_StartingAProgram_Asks()
    {
        var user = await Goal("/User.goal", Make.Step("start it", Start(Program)));
        var ran = await _app.Start(user, Context);
        await Assert.That(ran.Success).IsFalse().Because("a user goal is asked, and the answer was no");
        await Assert.That(ran.Error!.Key).IsEqualTo("PermissionDenied");
    }

    [Test]
    public async Task AnOsGoal_StartingAProgramItNames_StartsItUnasked()
    {
        var os = await OsGoal("Os.goal", Make.Step("start it", Start(Program)));
        var ran = await _app.Start(os, Context);
        await ran.IsSuccess();
        await Assert.That(await Context.Actor!.Permission.Find(global::app.type.item.path.file.@this.Resolve(Program, Context),
            global::app.type.item.permission.Verb.execute)).IsNull().Because("nothing is stored: the trust is the goal's origin, not a grant");
    }

    [Test]
    public async Task AnOsGoal_HandedItsProgramByItsCaller_Asks()
    {
        await Context.Variable.Set("program", new global::app.data.@this("program", Program, context: Context));
        var os = await OsGoal("Run.goal", Make.Step("start what it was given", Start("%program%")));
        var ran = await _app.Start(os, Context);
        await Assert.That(ran.Success).IsFalse().Because("a program the caller gave is the caller's to answer for");
        await Assert.That(ran.Error!.Key).IsEqualTo("PermissionDenied");
    }

    [Test]
    public async Task AUserGoal_CalledBackFromAnOsGoal_Asks()
    {
        await Goal("/Callback.goal", Make.Step("start it", Start(Program)));
        var os = await OsGoal("Calls.goal",
            Make.Step("call the user's goal", Make.Action(Context, "goal", "call", ("Name", "/Callback"))));
        var ran = await _app.Start(os, Context);
        await Assert.That(ran.Success).IsFalse().Because("the step asking is the user's goal's, whoever called it");
        await Assert.That(ran.Error!.Key).IsEqualTo("PermissionDenied");
    }
}
