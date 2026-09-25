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
        var loaded = await Load("start.pr", "{\"name\":\"Start\",\"step\":[{\"index\":0,\"text\":\"a\",\"warning\":[],\"action\":[]}]}");

        await Assert.That(loaded.Error?.Key).IsEqualTo("PrFormatOutdated");
        await Assert.That(loaded.Error!.Message).Contains("step key 'warning' isn't in this .pr format");
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
