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

    // ---- writing where plang's own goals are: asked, and the question says what a yes means (Ingi: ask, don't refuse) ----

    // an input channel that keeps each question and answers it "n"
    private sealed class Questions : global::app.channel.@this
    {
        public List<string> Asked { get; } = new();
        public Questions() { Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override async Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Asked.Add((await action.Question!.Value())?.ToString() ?? "");
            return action.Context.Ok("n");
        }
    }

    [Test]
    [Arguments(global::app.type.item.permission.Verb.write)]
    [Arguments(global::app.type.item.permission.Verb.delete)]
    public async Task ChangingAFileUnderTheOsFolder_IsAsked_SayingWhatAYesMeans(global::app.type.item.permission.Verb verb)
    {
        var questions = new Questions();
        Context.Actor!.Channel.Register(questions);
        var file = new global::app.type.item.path.file.@this(Os("Changed.goal"));
        var asked = await file.Authorize(verb, Context);
        await asked.IsFailure();
        await Assert.That(questions.Asked.Count).IsEqualTo(1).Because("asked, not refused");
        await Assert.That(questions.Asked[0]).Contains("ship with plang").And.Contains("without asking");
    }

    [Test]
    public async Task ReadingUnderTheOsFolder_OrWritingInTheAppsRoot_SaysNothingOfIt()
    {
        var questions = new Questions();
        Context.Actor!.Channel.Register(questions);
        await (await new global::app.type.item.path.file.@this(Path.Combine(_root, "own.txt")).Authorize(global::app.type.item.permission.Verb.write, Context)).IsSuccess();
        await new global::app.type.item.path.file.@this(Os("Read.goal")).Authorize(global::app.type.item.permission.Verb.read, Context);
        await Assert.That(questions.Asked.Any(q => q.Contains("ship with plang"))).IsFalse();
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

    // an os goal echoing what it names: the app's root anchor, or a plain variable
    private global::app.goal.step.action.@this Echo(string argument)
        => Make.Action(Context, "terminal", "start", ("App", "//bin/echo"), ("Parameter", new List<object?> { argument }));

    [Test]
    public async Task AnOsGoal_NamingTheAppsRootAnchor_StartsUnasked_WithTheRealRoot()
    {
        var os = await OsGoal("Anchor.goal", Make.Step("echo the root", Echo("%!app.AbsolutePath%")));
        var ran = await _app.Start(os, Context);
        await ran.IsSuccess();
        await Assert.That((await ran.Value())?.ToString()?.TrimEnd('/')).IsEqualTo(_root.TrimEnd('/'));
    }

    [Test]
    public async Task TheRootAnchor_CantBeSetByAUserGoal()
    {
        // a user goal tries to point the anchor elsewhere, then calls the os goal that names it
        var os = await OsGoal("Anchored.goal", Make.Step("echo the root", Echo("%!app.AbsolutePath%")));
        var user = await Goal("/Shadow.goal",
            Make.Step("point the root elsewhere", Make.Action(Context, "variable", "set",
                Make.Param(Context, "Name", "!app.AbsolutePath", "variable"), ("Value", "/"))),
            Make.Step("call the os goal", Make.Action(Context, "goal", "call", ("Name", Os("Anchored")))));
        var ran = await _app.Start(user, Context);
        await Assert.That(ran.Success).IsFalse().Because("the app's own member can't be set over");
        await Assert.That(ran.Error!.Key).IsEqualTo("OwnMember");
        // and after the try, the os goal still starts with the real root, unasked
        var after = await _app.Start(os, Context);
        await after.IsSuccess();
        await Assert.That((await after.Value())?.ToString()?.TrimEnd('/')).IsEqualTo(_root.TrimEnd('/'));
    }

    [Test]
    public async Task AnOsGoal_NamingAPlainVariable_Asks()
    {
        await Context.Variable.Set("dir", new global::app.data.@this("dir", _root, context: Context));
        var os = await OsGoal("Plain.goal", Make.Step("echo a variable", Echo("%dir%")));
        var ran = await _app.Start(os, Context);
        await Assert.That(ran.Success).IsFalse().Because("a plain variable is the caller's");
        await Assert.That(ran.Error!.Key).IsEqualTo("PermissionDenied");
    }

    // the user's terminal setting gives every program a variable — what a user program could do with LD_PRELOAD
    private global::app.goal.step.action.@this SetMark()
        => Make.Action(Context, "variable", "set", Make.Param(Context, "Name", "%!terminal.setting.environment%", "variable"),
            ("Value", new Dictionary<string, object?> { ["PLANG_MARK"] = "leaked" }));

    // says only whether the mark reached the program — never the environment itself (it holds the box's secrets)
    private global::app.goal.step.action.@this Env() => Make.Action(Context, "terminal", "start", ("App", "//bin/sh"),
        ("Parameter", new List<object?> { "-c", "if [ -n \"$PLANG_MARK\" ]; then echo marked; else echo clean; fi" }));

    [Test]
    public async Task AnOsStart_GivenAnOptionByTheActorsSettings_Asks()
    {
        // the os step writes no Environment: the user's setting would give it one — the start is then the actor's
        await OsGoal("Env.goal", Make.Step("show the environment", Env()));
        var user = await Goal("/SetsMark.goal",
            Make.Step("set the setting", SetMark()),
            Make.Step("call the os goal", Make.Action(Context, "goal", "call", ("Name", "/system/trust/Env"))));
        var ran = await _app.Start(user, Context);
        await Assert.That(ran.Success).IsFalse().Because("an option a setting gives is the actor's choice: asked, and the answer was no");
        await Assert.That(ran.Error!.Key).IsEqualTo("PermissionDenied");
    }

    [Test]
    public async Task ATrustedStart_TakesNoEnvironmentFromTheActorsSettings()
    {
        // the os step writes its own Environment: trusted, and the setting's environment is not merged in
        await OsGoal("OwnEnv.goal", Make.Step("show the environment", Make.Action(Context, "terminal", "start", ("App", "//bin/sh"),
            ("Parameter", new List<object?> { "-c", "if [ -n \"$PLANG_MARK\" ]; then echo marked; else echo clean; fi" }),
            ("Environment", new Dictionary<string, object?> { ["PLANG_OWN"] = "1" }))));
        var user = await Goal("/SetsMarkToo.goal",
            Make.Step("set the setting", SetMark()),
            Make.Step("call the os goal", Make.Action(Context, "goal", "call", ("Name", "/system/trust/OwnEnv"))));
        var ran = await _app.Start(user, Context);
        await ran.IsSuccess();
        await Assert.That((await ran.Value())?.ToString()?.Trim()).IsEqualTo("clean")
            .Because("what starts unasked takes nothing of the actor's settings that changes what runs");
    }

    [Test]
    public async Task AUsersOwnStart_StillTakesItsSetting()
    {
        Context.Actor!.Channel.Register(new CannedAnswerChannel("a"));
        var user = await Goal("/OwnEnv.goal", Make.Step("set the setting", SetMark()), Make.Step("show the environment", Env()));
        var ran = await _app.Start(user, Context);
        await ran.IsSuccess();
        await Assert.That((await ran.Value())?.ToString()?.Trim()).IsEqualTo("marked");
    }

    // the actor's output channel, captured: what an echoing start writes there
    private MemoryStream CaptureOutput()
    {
        var captured = new MemoryStream();
        Context.Actor!.Channel.Register(new global::app.channel.type.stream.@this(global::app.channel.list.@this.Output, captured,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain" });
        return captured;
    }

    private global::app.goal.step.action.@this SetEcho()
        => Make.Action(Context, "variable", "set", Make.Param(Context, "Name", "%!terminal.setting.echo%", "variable"), ("Value", true));

    private global::app.goal.step.action.@this Say(string word)
        => Make.Action(Context, "terminal", "start", ("App", "//bin/sh"), ("Parameter", new List<object?> { "-c", "echo " + word }));

    [Test]
    public async Task ATrustedStart_RunsByTheTerminalsDefaults_NotTheActorsSettings()
    {
        // the user's Echo would write a trusted program's output onto the actor's output (in PlangOS, the host's frame pipe)
        var captured = CaptureOutput();
        await OsGoal("Says.goal", Make.Step("say it", Say("trusted-word")));
        var user = await Goal("/SetsEcho.goal",
            Make.Step("set echo", SetEcho()),
            Make.Step("call the os goal", Make.Action(Context, "goal", "call", ("Name", "/system/trust/Says"))));
        var ran = await _app.Start(user, Context);
        await ran.IsSuccess();
        await Assert.That(System.Text.Encoding.UTF8.GetString(captured.ToArray())).DoesNotContain("trusted-word")
            .Because("a trusted start reads the terminal's defaults, none of the actor's settings");
    }

    [Test]
    public async Task AUsersOwnStart_StillEchoes()
    {
        var captured = CaptureOutput();
        Context.Actor!.Channel.Register(new CannedAnswerChannel("a"));
        var user = await Goal("/OwnEcho.goal", Make.Step("set echo", SetEcho()), Make.Step("say it", Say("own-word")));
        var ran = await _app.Start(user, Context);
        await ran.IsSuccess();
        await Assert.That(System.Text.Encoding.UTF8.GetString(captured.ToArray())).Contains("own-word");
    }

    // ---- Clean and Keep: a program's environment, checked by a marker only ----

    // a start that says whether plang's own variable <paramref name="secret"/> reached it, with what the step adds
    private global::app.goal.step.action.@this Sees(string secret, params (string, object?)[] more)
    {
        var parameters = new List<(string, object?)> { ("App", "//bin/sh"),
            ("Parameter", new List<object?> { "-c", "if [ -n \"$" + secret + "\" ]; then echo marked; else echo clean; fi" }) };
        parameters.AddRange(more);
        return Make.Action(Context, "terminal", "start", parameters.ToArray());
    }

    // runs a user goal made by <paramref name="steps"/>, every ask answered yes, with plang's own environment holding a
    // variable of its own (tests run side by side: a process-wide name shared between them would be unset mid-run)
    private async Task<global::app.data.@this> RunWithSecret(Func<string, Make.StepDef[]> steps)
    {
        var secret = "PLANG_TEST_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Context.Actor!.Channel.Register(new CannedAnswerChannel("a"));
        System.Environment.SetEnvironmentVariable(secret, "s");
        try { return await _app.Start(await Goal("/Env" + Guid.NewGuid().ToString("N")[..6] + ".goal", steps(secret)), Context); }
        finally { System.Environment.SetEnvironmentVariable(secret, null); }
    }

    [Test]
    public async Task ACleanStart_HasNoneOfPlangsEnvironment_AndAPlainStartHasIt()
    {
        var plain = await RunWithSecret(s => [Make.Step("plain", Sees(s))]);
        await Assert.That((await plain.Value())?.ToString()?.Trim()).IsEqualTo("marked").Because("a plain start inherits plang's environment");
        var clean = await RunWithSecret(s => [Make.Step("clean", Sees(s, ("Clean", true)))]);
        await Assert.That((await clean.Value())?.ToString()?.Trim()).IsEqualTo("clean").Because("a clean start keeps nothing it isn't told to");
    }

    [Test]
    public async Task Keep_CopiesTheNamedVariablesFromPlangsEnvironment()
    {
        var kept = await RunWithSecret(s => [Make.Step("clean, keeping it", Sees(s, ("Clean", true), ("Keep", new List<object?> { s })))]);
        await Assert.That((await kept.Value())?.ToString()?.Trim()).IsEqualTo("marked");
    }

    [Test]
    public async Task Keep_RefusesWhatIsNoVariableName()
    {
        var bad = await RunWithSecret(s => [Make.Step("keep a value", Sees(s, ("Clean", true), ("Keep", new List<object?> { "A=B" })))]);
        await Assert.That(bad.Success).IsFalse();
        await Assert.That(bad.Error!.Key).IsEqualTo("KeepInvalid");
    }

    [Test]
    public async Task ACleanStart_TakesNoEnvironmentSetting_AndAnOsOneStaysTrusted()
    {
        // an os goal starting clean, writing no Environment: the user's environment setting is not taken, and it is not asked
        await OsGoal("Clean.goal", Make.Step("start clean", Make.Action(Context, "terminal", "start", ("App", "//bin/sh"),
            ("Parameter", new List<object?> { "-c", "if [ -n \"$PLANG_MARK\" ]; then echo marked; else echo clean; fi" }), ("Clean", true))));
        var user = await Goal("/SetsMarkClean.goal",
            Make.Step("set the setting", SetMark()),
            Make.Step("call the os goal", Make.Action(Context, "goal", "call", ("Name", "/system/trust/Clean"))));
        var ran = await _app.Start(user, Context);
        await ran.IsSuccess();
        await Assert.That((await ran.Value())?.ToString()?.Trim()).IsEqualTo("clean");
    }

    [Test]
    public async Task AnOsGoal_KeepingWhatItWasHanded_Asks()
    {
        await Context.Variable.Set("names", new global::app.data.@this("names", new List<object?> { "HOME" }, context: Context));
        var os = await OsGoal("KeepsHanded.goal", Make.Step("keep what it was given", Make.Action(Context, "terminal", "start",
            ("App", "//bin/true"), ("Clean", true), ("Keep", "%names%"))));
        var ran = await _app.Start(os, Context);
        await Assert.That(ran.Success).IsFalse().Because("names the caller gave are the caller's");
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
