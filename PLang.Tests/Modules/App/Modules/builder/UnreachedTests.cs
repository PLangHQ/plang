namespace PLang.Tests.App.Modules.builder;

// A file's private goals are called only from that file, so what reaches one is the file's public goal and
// what it calls — a call, an on.event binding, a channel's goal, an on.error recovery. build.unreached warns,
// on the builder channel, about each private goal nothing reaches; a public goal is never warned, and a file
// whose call names its goal only at run warns about nothing.
public class UnreachedTests : System.IAsyncDisposable
{
    private readonly global::app.@this app;
    private readonly System.IO.MemoryStream warnings = new();

    public UnreachedTests()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "unreached-" + System.Guid.NewGuid().ToString("N")[..6]);
        System.IO.Directory.CreateDirectory(root);
        app = new global::app.@this(root).Testing();
        app.actor.list.User.Channel.Register(new global::app.channel.type.stream.@this(
            "builder", warnings, global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain" });
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    private global::app.goal.step.action.@this Write(string text) => Make.Action(Ctx, "output", "write", ("Data", text));

    // A private goal of /Main.goal; by default it writes its name.
    private global::app.goal.@this Sub(string name, params global::PLang.Tests.Shared.Make.StepDef[] steps)
        => Make.Goal(Ctx, name, "/Main.goal", steps.Length > 0 ? steps : [Make.Step("write out " + name, Write(name))]);

    // One file: its public goal Main with the given steps, and its private goals — read back as a built .pr.
    private async Task<global::app.goal.@this> File(global::PLang.Tests.Shared.Make.StepDef[] main, params global::app.goal.@this[] subs)
    {
        var file = Make.Goal(Ctx, "Main", "/Main.goal", main);
        foreach (var sub in subs) file.Child.Add(sub);
        var loaded = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, file);
        app.goal.list.Add(loaded);
        return loaded;
    }

    private async Task<global::app.goal.@this> File(global::PLang.Tests.Shared.Make.StepDef[] main, params string[] subs)
        => await File(main, subs.Select(s => Sub(s)).ToArray());

    private static global::PLang.Tests.Shared.Make.StepDef[] Steps(params global::PLang.Tests.Shared.Make.StepDef[] steps) => steps;

    // Each warning's message, as the builder channel wrote it ({action, key, message} per line).
    private string Written => string.Join("\n", System.Text.Encoding.UTF8.GetString(warnings.ToArray())
        .Split('\n', System.StringSplitOptions.RemoveEmptyEntries)
        .Select(line => System.Text.Json.JsonDocument.Parse(line).RootElement)
        .Select(w => $"{w.GetProperty("key").GetString()}: {w.GetProperty("message").GetString()}"));

    private async Task Unreached() => await (await Make.Action(Ctx, "build", "unreached").Start(Ctx)).IsSuccess();

    [Test] public async Task ASubGoalNobodyCalls_IsWarned()
    {
        var file = await File(Steps(Make.Step("write out hi", Write("hi"))), "Lonely");

        await Assert.That((await file.Unreached(Ctx)).Select(u => u.Goal.Name)).IsEquivalentTo(["Lonely"]);
        await Unreached();
        await Assert.That(Written).Contains("GoalUnreached");
        await Assert.That(Written).Contains("'Lonely' in /Main.goal is not reached by any goal");
    }

    [Test] public async Task ASubGoalTheFileCalls_IsNotWarned()
    {
        var file = await File(Steps(Make.Step("call Sub", Make.Call(Ctx, "Sub"))), "Sub");

        await Assert.That(await file.Unreached(Ctx)).IsEmpty();
        await Unreached();
        await Assert.That(Written).DoesNotContain("GoalUnreached");
    }

    [Test] public async Task ASubGoalReachedOnlyThroughAnEventBinding_IsNotWarned()
    {
        var file = await File(Steps(Make.Step("on each goal start, call Audit",
            Make.Action(Ctx, "on", "event", ("Event", "%!app.type.goal.on.start%"), ("When", "after"),
                ("Action", Make.Call(Ctx, "Audit"))))), "Audit");

        await Assert.That(await file.Unreached(Ctx)).IsEmpty();
    }

    [Test] public async Task ASubGoalReachedOnlyThroughAChannel_IsNotWarned()
    {
        var file = await File(Steps(Make.Step("set channel \"log\" call Log",
            Make.Action(Ctx, "channel", "set", ("Name", "log"), ("Goal", Make.Call(Ctx, "Log"))))), "Log");

        await Assert.That(await file.Unreached(Ctx)).IsEmpty();
    }

    [Test] public async Task ASubGoalReachedOnlyThroughAnErrorRecovery_IsNotWarned()
    {
        var file = await File(Steps(Make.Step("write out hi, on error call Recover", Write("hi"),
            Make.Action(Ctx, "on", "error", Make.Recovery(Ctx, Make.Call(Ctx, "Recover"))))), "Recover");

        await Assert.That(await file.Unreached(Ctx)).IsEmpty();
    }

    [Test] public async Task AFileWithADynamicCall_WarnsAboutNothing()
    {
        var file = await File(Steps(Make.Step("call %which%", Make.Call(Ctx, "%which%"))), "Lonely");

        await Assert.That(await file.Unreached(Ctx)).IsEmpty();
        await Unreached();
        await Assert.That(Written).DoesNotContain("GoalUnreached");
    }

    [Test] public async Task APublicGoalNothingCalls_IsNeverWarned()
    {
        await File(Steps(Make.Step("write out hi", Write("hi"))), System.Array.Empty<global::app.goal.@this>());

        await Unreached();
        await Assert.That(Written).DoesNotContain("GoalUnreached");
    }

    [Test] public async Task ASubGoalReachedOnlyFromAnUnreachedOne_SaysSo()
    {
        var file = await File(Steps(Make.Step("write out hi", Write("hi"))),
            Sub("Bind", Make.Step("call Handler", Make.Call(Ctx, "Handler"))), Sub("Handler"));

        await Assert.That((await file.Unreached(Ctx)).Select(u => $"{u.Goal.Name}<{u.From?.Name}"))
            .IsEquivalentTo(["Bind<", "Handler<Bind"]);
        await Unreached();
        await Assert.That(Written).Contains("'Bind' in /Main.goal is not reached by any goal");
        await Assert.That(Written).Contains("'Handler' in /Main.goal is reached only from 'Bind', which nothing reaches");
    }

    [Test] public async Task APrivateGoal_AnswersNone()
    {
        var file = await File(Steps(Make.Step("write out hi", Write("hi"))), "Lonely");

        await Assert.That(await file.Child.Items().First().Unreached(Ctx)).IsEmpty();
    }
}
