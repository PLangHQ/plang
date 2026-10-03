namespace PLang.Tests.App.Goals;

/// <summary>
/// A .pr loads as the goal its builder wrote, or is refused by name — never as an empty goal. A key
/// the reader doesn't know means another builder wrote the file: <c>PrFormatOutdated</c>, naming the
/// file and the key. The live system .pr files the runtime loads today still load.
/// </summary>
public class PrLoadTests : System.IAsyncDisposable
{
    private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "prload-" + System.Guid.NewGuid().ToString("N")[..8]);
    private readonly global::app.@this _app;

    public PrLoadTests()
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_root, ".build"));
        _app = new global::app.@this(_root).Testing();
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private async Task<global::app.data.@this> Load(string file, string pr)
    {
        System.IO.File.WriteAllText(System.IO.Path.Combine(_root, ".build", file), pr);
        return await _app.goal.Load("/" + System.IO.Path.ChangeExtension(file, ".goal"));
    }

    [Test]
    public async Task AV01Pr_IsRefused_NamingTheFileAndTheKey()
    {
        var loaded = await Load("start.pr", "{\"GoalName\":\"Start\",\"GoalSteps\":[{\"Text\":\"a\"}]}");

        await Assert.That(loaded.Success).IsFalse();
        await Assert.That(loaded.Error!.Key).IsEqualTo("PrFormatOutdated");
        await Assert.That(loaded.Error.Message).Contains("start.pr: key 'GoalName' isn't in this .pr format");
        await Assert.That(loaded.Error.Message).Contains("Rebuild the goal.");
    }

    [Test]
    public async Task AnUnknownStepKey_IsRefused_NamingIt()
    {
        var loaded = await Load("start.pr", "{\"name\":\"Start\",\"step\":[{\"index\":0,\"text\":\"a\",\"ModuleType\":\"x\",\"code\":[]}]}");

        await Assert.That(loaded.Error?.Key).IsEqualTo("PrFormatOutdated");
        await Assert.That(loaded.Error!.Message).Contains("step key 'ModuleType' isn't in this .pr format");
        // the step's reader names the file itself: it was born under the read that knew it
        await Assert.That(loaded.Error.Message).Contains("start.pr: step key");
    }

    // A .pr whose content doesn't read as a goal is refused under a key of its own, never a bare exception.
    [Test]
    public async Task AnUntypedProperty_IsRefusedUnderAKey()
    {
        var loaded = await Load("start.pr",
            "{\"name\":\"Start\",\"step\":[{\"index\":0,\"text\":\"a\",\"code\":[{\"module\":\"file\",\"name\":\"read\",\"property\":[{\"name\":\"Path\",\"value\":\"a.txt\"}]}]}]}");

        await Assert.That(loaded.Error?.Key).IsEqualTo("MaterializeFailed");
    }

    // A step's actions are its `code`; a .pr that holds them under `action` was built by an older builder.
    [Test]
    public async Task AnActionKeyPr_IsRefused_NamingAction()
    {
        var loaded = await Load("start.pr", "{\"name\":\"Start\",\"step\":[{\"index\":0,\"text\":\"a\",\"action\":[]}]}");

        await Assert.That(loaded.Error?.Key).IsEqualTo("PrFormatOutdated");
        await Assert.That(loaded.Error!.Message).Contains("step key 'action' isn't in this .pr format");
    }

    // A .pr built before a property went (goal.call's Wait) names a property its action no longer has: run, the
    // step fails naming it — never runs with it dropped, doing something else.
    [Test]
    public async Task APropertyItsActionHasNot_FailsTheRun_NamingIt()
    {
        var context = _app.actor.list.User.Context;
        var built = Make.Goal(context, "Start", "/Start.goal",
            Make.Step("call Other", Make.Action(context, "goal", "call", ("Name", "Other"))));
        var pr = System.Text.Json.Nodes.JsonNode.Parse(await context.Pr(built))!;
        var property = pr["step"]![0]!["code"]![0]!["property"]!.AsArray();
        var wait = property[0]!.DeepClone();
        wait["name"] = "Wait";
        property.Add(wait);
        var loaded = await Load("start.pr", pr.ToJsonString());
        await loaded.IsSuccess();

        var ran = await ((global::app.goal.@this)(await loaded.Value())!).Start(context);

        await Assert.That(ran.Success).IsFalse();
        await Assert.That(ran.Error!.Message).Contains("goal.call has no property Wait; rebuild the goal");
    }

    // A one-step goal saved as a .pr (as `file`), one row of its action written as an older build wrote it — the step's
    // own (`property`) or one the build froze (`default`) — then loaded through the real load path and run.
    private async Task<global::app.data.@this> RanWith(string file, global::app.goal.step.action.@this action, string section, string row)
    {
        var context = _app.actor.list.User.Context;
        var pr = System.Text.Json.Nodes.JsonNode.Parse(await context.Pr(Make.Goal(context, "Start", "/Start.goal", Make.Step("a step", action))))!;
        var code = pr["step"]![0]!["code"]![0]!.AsObject();
        if (code[section] is not System.Text.Json.Nodes.JsonArray rows) code[section] = rows = [];
        var written = System.Text.Json.Nodes.JsonNode.Parse(row)!;
        foreach (var old in rows.Where(r => string.Equals((string?)r!["name"], (string?)written["name"], StringComparison.OrdinalIgnoreCase)).ToList())
            rows.Remove(old);
        rows.Add(written);
        var loaded = await Load(file, pr.ToJsonString());
        await loaded.IsSuccess();
        return await ((global::app.goal.@this)(await loaded.Value())!).Start(context);
    }

    // A .pr built when list.split's Empty was a bool froze `true` for it; Empty is a choice now. Run, the step fails
    // saying to rebuild — never reads today's default in its place, running differently from how it was built.
    [Test]
    public async Task ADefaultFrozenInATypeItsOptionIsNoLonger_FailsTheRun_SayingRebuild()
    {
        var ran = await RanWith("split.pr", Make.Action(_app.actor.list.User.Context, "list", "split", ("Value", "a\nb")),
            "default", "{\"name\":\"empty\",\"type\":{\"name\":\"bool\"},\"value\":true}");

        await Assert.That(ran.Error?.Key).IsEqualTo("Stale");
        await Assert.That(ran.Error!.Message).Contains("list.split was built when Empty was a bool; it is a choice now — rebuild the goal");
    }

    // The step's own row the same: file.read's Template written `true`, before Template was a choice
    [Test]
    public async Task ALiteralTheStepSetInATypeItsOptionIsNoLonger_FailsTheRun_SayingRebuild()
    {
        var ran = await RanWith("read.pr", Make.Action(_app.actor.list.User.Context, "file", "read", ("Path", "note.txt")),
            "property", "{\"name\":\"Template\",\"type\":{\"name\":\"bool\"},\"value\":true}");

        await Assert.That(ran.Error?.Key).IsEqualTo("Stale");
        await Assert.That(ran.Error!.Message).Contains("file.read was built when Template was a bool; it is a choice now — rebuild the goal");
    }

    // What the run takes is never stale: goal.call's Name is a goal's name, selected at run; a bool its option makes
    // itself from (goal.call's Parallel, a parallel now) runs as built
    [Test]
    public async Task ANameTheRunSelects_AndAValueItsOptionMakesItselfFrom_AreNotStale()
    {
        var context = _app.actor.list.User.Context;
        _app.goal.list.Add(await RealGoalLoad.ViaChannel(_app, Make.Goal(context, "Other", "/Other.goal",
            Make.Step("write", Make.Action(context, "output", "write", ("Data", "hi"))))));

        var ran = await RanWith("call.pr", Make.Action(context, "goal", "call", ("Name", "Other")),
            "default", "{\"name\":\"parallel\",\"type\":{\"name\":\"bool\"},\"value\":false}");

        await ran.IsSuccess();
    }

    // A literal its option declines in its own type is a bad value, as the build names it — not a stale build
    [Test]
    public async Task ABadLiteralOfItsOptionsOwnType_KeepsItsOwnError()
    {
        var ran = await RanWith("bad.pr", Make.Action(_app.actor.list.User.Context, "list", "split", ("Value", "a\nb")),
            "property", "{\"name\":\"Empty\",\"type\":{\"name\":\"choice\",\"kind\":\"empty\"},\"value\":\"sometimes\"}");

        await Assert.That(ran.Success).IsFalse();
        await Assert.That(ran.Error!.Key).IsNotEqualTo("Stale");
    }

    // A small goal saved as a .pr and loaded through the real load path runs: its variable is set and
    // its output written. Its indented step and its warning ride the .pr and come back.
    [Test]
    public async Task ASmallGoal_SavedAndLoaded_Runs()
    {
        var built = Make.Goal(_app.actor.list.User.Context, "Start", "/Start.goal",
            Make.Step("set %n% = 5", Make.Action(_app.actor.list.User.Context, "variable", "set", Make.Param(_app.actor.list.User.Context, "Name", "n", "variable"), ("Value", 5))),
            Make.Step("write out \"n is %n%\"", Make.Action(_app.actor.list.User.Context, "output", "write", ("Data", "n is %n%"))));
        built.Step[1].Line = new() { Indent = 1 };
        built.Step[1].Warning.Add(new global::app.warning.@this { Key = "Unsure", Message = "step 1 uses output.write" });
        var output = new System.IO.MemoryStream();
        _app.actor.list.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, output, ChannelDirection.Output, ownsStream: true) { Mime = "text/plain" });

        var goal = await RealGoalLoad.ViaChannel(_app, built);
        var ran = await goal.Start(_app.actor.list.User.Context);

        await ran.IsSuccess();
        await Assert.That(System.Text.Encoding.UTF8.GetString(output.ToArray())).Contains("n is 5");
        await Assert.That((await (await _app.actor.list.User.Context.Variable.Get("n")).Value())?.ToString()).IsEqualTo("5");
        await Assert.That(goal.Step[1].Line.Indent).IsEqualTo(1);
        await Assert.That(goal.Step[1].Warning.Single().Key).IsEqualTo("Unsure");
    }


    [Test]
    public async Task AGoalWithNoName_IsRefused()
    {
        var loaded = await Load("start.pr", "{\"step\":[]}");

        await Assert.That(loaded.Error?.Key).IsEqualTo("PrFormatOutdated");
        await Assert.That(loaded.Error!.Message).Contains("it has no 'name'");
    }

    // The runtime loads /system/test.goal for `plang --test`; /system/error/Show.goal is loaded and run by
    // ErrorShowTests.
    [Test]
    [Arguments("/system/test.goal", 4)]
    [Arguments("/system/error/Show.goal", 3)]
    public async Task TheLiveSystemPr_StillLoads(string source, int steps)
    {
        await using var os = new global::app.@this(System.IO.Path.Combine(RepoRoot(), "os")).Testing();

        var loaded = await os.goal.Load(source);

        await loaded.IsSuccess();
        await Assert.That((await loaded.Value() as global::app.goal.@this)!.Step.Count).IsEqualTo(steps);
    }

    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "PLang", "app")))
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        return dir!;
    }
}
