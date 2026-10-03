using PLang.Tests.App.Types.PathTests.Contract;

namespace PLang.Tests.App.actions.terminal;

/// <summary>
/// What a program started through the terminal does, as a goal sees it: <c>terminal.start</c> runs it to its end — its
/// stdout the value, its exit code and stderr beside it, its lines to OnOutput and OnError as they come, Input on its
/// stdin, stopped at the timeout; <c>terminal.open</c> keeps it running — <c>send</c> writes a line to it, <c>wait</c>
/// gives its exit code once its last lines are delivered, <c>stop</c> ends it; binary output arrives message by message.
/// </summary>
public class ProgramTests : IDisposable
{
    private readonly string _root;
    private readonly global::app.@this _app;

    public ProgramTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_program_" + Guid.NewGuid().ToString("N"))).FullName;
        _app = new global::app.@this(_root).Testing();
        Context.Actor!.Channel.Register(new CannedAnswerChannel("a"));
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Directory.Delete(_root, true);
    }

    private global::app.actor.context.@this Context => _app.actor.list.User.Context;

    private global::app.goal.step.action.@this Sh(string verb, string script, params (string, object?)[] more)
    {
        var parameters = new List<(string, object?)> { ("App", "//bin/sh"), ("Parameter", new List<object?> { "-c", script }) };
        parameters.AddRange(more);
        return Make.Action(Context, "terminal", verb, parameters.ToArray());
    }

    private global::app.goal.step.action.@this Process(string verb, params (string, object?)[] more)
    {
        var parameters = new List<(string, object?)> { Make.Param(Context, "Process", "%program%", "variable") };
        parameters.AddRange(more);
        return Make.Action(Context, "terminal", verb, parameters.ToArray());
    }

    // the step's result kept as %name%
    private global::app.goal.step.action.@this Kept(string name)
        => Make.Action(Context, "variable", "set", Make.Param(Context, "Name", "%" + name + "%", "variable"), ("Value", "%!data%"));

    // a goal each line goes to: it adds %!data% to %lines%
    private async Task Collects(string name)
    {
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(Context, name, "/" + name + ".goal",
            Make.Step("add it", Make.Action(Context, "list", "add", Make.Param(Context, "ListName", "lines", "variable"), ("Value", "%!data%")))));
        _app.goal.list.Add(goal);
    }

    private async Task<global::app.data.@this> Run(params Make.StepDef[] steps)
    {
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(Context, "Run" + Guid.NewGuid().ToString("N")[..6], steps));
        _app.goal.list.Add(goal);
        return await _app.Start(goal, Context);
    }

    private async Task<List<string?>> Lines()
    {
        var rows = new List<string?>();
        if (await (await Context.Variable.Get("lines")).Value() is global::app.type.item.list.@this list)
            foreach (var row in list.Items(Context)) rows.Add((await row.Value())?.ToString());
        return rows;
    }

    [Test]
    public async Task Start_GivesItsStdout_WithItsExitCodeAndStderrBeside()
    {
        if (!OperatingSystem.IsLinux()) return;
        var ran = await Run(Make.Step("run it", Sh("start", "echo out; echo err >&2; exit 4")));
        await ran.IsSuccess();
        await Assert.That((await ran.Value())?.ToString()).IsEqualTo("out");
        await Assert.That((await ran.Properties.Get<object>("ExitCode"))?.ToString()).IsEqualTo("4");
        await Assert.That((await ran.Properties.Get<object>("Error"))?.ToString()).IsEqualTo("err");
        await Assert.That((await ran.Properties.Get<object>("TimedOut"))?.ToString()).IsEqualTo("False");
    }

    [Test]
    public async Task Start_WritesInput_OnItsStdin()
    {
        if (!OperatingSystem.IsLinux()) return;
        var ran = await Run(Make.Step("run it", Sh("start", "cat", ("Input", "hello there"))));
        await Assert.That((await ran.Value())?.ToString()).IsEqualTo("hello there");
    }

    [Test]
    public async Task Start_GivesEachLine_ToOnOutputAndOnError_InOrder()
    {
        if (!OperatingSystem.IsLinux()) return;
        await Collects("Collect");
        var ran = await Run(Make.Step("run it", Sh("start", "echo a; echo b; echo c >&2",
            ("OnOutput", Make.Call(Context, "Collect")), ("OnError", Make.Call(Context, "Collect")))));
        await ran.IsSuccess();
        await Assert.That(await Lines()).IsEquivalentTo(new[] { "a", "b", "c" });
    }

    [Test]
    public async Task Start_IsStoppedAtTheTimeout()
    {
        if (!OperatingSystem.IsLinux()) return;
        var ran = await Run(
            Make.Step("set timeout", Make.Action(Context, "variable", "set", Make.Param(Context, "Name", "%!terminal.setting.timeoutinsec%", "variable"), ("Value", 1))),
            Make.Step("run it", Sh("start", "sleep 20")));
        await Assert.That((await ran.Properties.Get<object>("TimedOut"))?.ToString()).IsEqualTo("True");
        await Assert.That((await ran.Properties.Get<object>("ExitCode"))?.ToString()).IsEqualTo("-1");
    }

    [Test]
    public async Task Open_TakesWhatIsSent_AndWaitGivesItsExitCode_AfterItsLastLines()
    {
        if (!OperatingSystem.IsLinux()) return;
        await Collects("Heard");
        var ran = await Run(
            Make.Step("open it", Sh("open", "read l; echo \"got $l\"; echo bye; exit 5", ("OnOutput", Make.Call(Context, "Heard"))), Kept("program")),
            Make.Step("send hi", Process("send", ("Data", "hi"))),
            Make.Step("wait", Process("wait")));
        await ran.IsSuccess();
        await Assert.That((await ran.Value())?.ToString()).IsEqualTo("5");
        await Assert.That(await Lines()).IsEquivalentTo(new[] { "got hi", "bye" });
    }

    [Test]
    public async Task Open_IsEndedByStop()
    {
        if (!OperatingSystem.IsLinux()) return;
        var ran = await Run(
            Make.Step("open it", Sh("open", "sleep 30"), Kept("program")),
            Make.Step("stop it", Process("stop")),
            Make.Step("wait", Process("wait")));
        await ran.IsSuccess();
        await Assert.That((await ran.Value())?.ToString()).IsNotEqualTo("0").Because("it was killed, not ended by itself");
        var program = (await (await Context.Variable.Get("program")).Value()) as global::app.module.terminal.type.process.@this;
        await Assert.That(program!.Running).IsFalse();
    }

    [Test]
    public async Task Open_Binary_GivesEachMessage_AsItsBytes()
    {
        if (!OperatingSystem.IsLinux()) return;
        await Collects("Got");
        var ran = await Run(
            Make.Step("open it", Sh("open", "printf '\\003\\000\\000\\000abc\\002\\000\\000\\000de'",
                ("Binary", true), ("OnOutput", Make.Call(Context, "Got"))), Kept("program")),
            Make.Step("wait", Process("wait")));
        await ran.IsSuccess();
        var rows = new List<string>();
        if (await (await Context.Variable.Get("lines")).Value() is global::app.type.item.list.@this list)
            foreach (var row in list.Items(Context))
                rows.Add(System.Text.Encoding.ASCII.GetString(((await row.Value()) as global::app.type.item.binary.@this)?.RawBytes ?? []));
        await Assert.That(rows).IsEquivalentTo(new[] { "abc", "de" });
    }
}
