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
        _app = TestApp.Create(_root);
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private async Task<global::app.data.@this> Load(string file, string pr)
    {
        System.IO.File.WriteAllText(System.IO.Path.Combine(_root, ".build", file), pr);
        return await _app.Goal.Load("/.build/" + file);
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
    }

    // A step's actions are its `code`; a .pr that holds them under `action` was built by an older builder.
    [Test]
    public async Task AnActionKeyPr_IsRefused_NamingAction()
    {
        var loaded = await Load("start.pr", "{\"name\":\"Start\",\"step\":[{\"index\":0,\"text\":\"a\",\"action\":[]}]}");

        await Assert.That(loaded.Error?.Key).IsEqualTo("PrFormatOutdated");
        await Assert.That(loaded.Error!.Message).Contains("step key 'action' isn't in this .pr format");
    }

    // A small goal saved as a .pr and loaded through the real load path runs: its variable is set and
    // its output written. Its indented step and its warning ride the .pr and come back.
    [Test]
    public async Task ASmallGoal_SavedAndLoaded_Runs()
    {
        var built = Make.Goal("Start", "/Start.goal",
            Make.Step("set %n% = 5", Make.Action("variable", "set", Make.Param("Name", "n", "variable"), ("Value", 5))),
            Make.Step("write out \"n is %n%\"", Make.Action("output", "write", ("Data", "n is %n%"))));
        built.Step[1].Line = new() { Indent = 1 };
        built.Step[1].Warning.Add(new global::app.warning.@this { Key = "Unsure", Message = "step 1 uses output.write" });
        var output = new System.IO.MemoryStream();
        _app.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, output, ChannelDirection.Output, ownsStream: true) { Mime = "text/plain" });

        var goal = await RealGoalLoad.ViaChannel(_app, built);
        var ran = await goal.Run(_app.User.Context);

        await ran.IsSuccess();
        await Assert.That(System.Text.Encoding.UTF8.GetString(output.ToArray())).Contains("n is 5");
        await Assert.That((await (await _app.User.Context.Variable.Get("n")).Value())?.ToString()).IsEqualTo("5");
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

    // The runtime loads /system/.build/test.pr for `plang --test`; /system/error/.build/show.pr is
    // loaded and run by ErrorShowTests.
    [Test]
    [Arguments("/system/.build/test.pr", 4)]
    [Arguments("/system/error/.build/show.pr", 3)]
    public async Task TheLiveSystemPr_StillLoads(string pr, int steps)
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));

        var loaded = await os.Goal.Load(pr);

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
