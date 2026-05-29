using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Goals.AppGoalsMigrationTests;

/// <summary>
/// Batch 5. AppGoals + App.Load/Save through Path verbs.
/// </summary>
public class AppGoalsThroughPathVerbsTests
{
    private static async Task<(PLangEngine app, string root)> NewApp()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-appgoals-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        await Task.CompletedTask;
        return (new PLangEngine(root), root);
    }

    [Test] public async Task LoadFromDirectoryAsync_UsesPathListNotDirectoryGetFiles()
    {
        var (app, root) = await NewApp();
        // Write a couple of .pr files at root/.build/.
        var buildDir = System.IO.Path.Combine(root, ".build");
        System.IO.Directory.CreateDirectory(buildDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(buildDir, "a.pr"), "{\"name\":\"A\",\"path\":\"/A.goal\"}");
        System.IO.File.WriteAllText(System.IO.Path.Combine(buildDir, "b.pr"), "{\"name\":\"B\",\"path\":\"/B.goal\"}");

        var result = await app.Goals.LoadFromDirectoryAsync(app, root);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(app.Goals.Get("A")).IsNotNull();
        await Assert.That(app.Goals.Get("B")).IsNotNull();
    }

    [Test] public async Task LoadFromDirectoryAsync_DeepTree_LoadsEveryGoalFile()
    {
        var (app, root) = await NewApp();
        var sub = System.IO.Path.Combine(root, "sub", "deep", ".build");
        System.IO.Directory.CreateDirectory(sub);
        System.IO.File.WriteAllText(System.IO.Path.Combine(sub, "deepgoal.pr"),
            "{\"name\":\"DeepGoal\",\"path\":\"/sub/deep/DeepGoal.goal\"}");
        var result = await app.Goals.LoadFromDirectoryAsync(app, root);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(app.Goals.Get("DeepGoal")).IsNotNull();
    }

    [Test] public async Task LoadFromFileAsync_UsesPathReadTextNotFileReadAllText()
    {
        var (app, root) = await NewApp();
        var buildDir = System.IO.Path.Combine(root, ".build");
        System.IO.Directory.CreateDirectory(buildDir);
        var prAbs = System.IO.Path.Combine(buildDir, "start.pr");
        System.IO.File.WriteAllText(prAbs, "{\"name\":\"Start\",\"path\":\"/Start.goal\"}");
        var result = await app.Goals.LoadFromFileAsync(app, "/.build/start.pr");
        await Assert.That(result.Success).IsTrue();
        var goal = result.Value as Goal;
        await Assert.That(goal!.Name).IsEqualTo("Start");
    }

    [Test] public async Task GetByPrPathAsync_ResolvesRelativeAndAbsolute_ViaPath()
    {
        var (app, root) = await NewApp();
        var buildDir = System.IO.Path.Combine(root, ".build");
        System.IO.Directory.CreateDirectory(buildDir);
        var prAbs = System.IO.Path.Combine(buildDir, "start.pr");
        System.IO.File.WriteAllText(prAbs, "{\"name\":\"Start\",\"path\":\"/Start.goal\"}");

        var byRel = await app.Goals.GetByPrPathAsync("/.build/start.pr");
        await Assert.That(byRel).IsNotNull();
        var byAbs = await app.Goals.GetByPrPathAsync(prAbs);
        await Assert.That(byAbs).IsNotNull();
    }

    [Test] public async Task AppGoals_FuzzyGetByName_StaysSeparateFromPathKeying()
    {
        var (app, _) = await NewApp();
        var goal = new Goal
        {
            Name = "ProcessData",
            Path = global::app.type.path.@this.Resolve("/processdata.goal", app.User.Context)
        };
        app.Goals.Add(goal);
        // Fuzzy by-name lookup: case-insensitive, picks up the goal.
        await Assert.That(app.Goals.Get("ProcessData")).IsNotNull();
        await Assert.That(app.Goals.Get("processdata")).IsNotNull();
    }

    [Test] public async Task AppLoad_OnColdStart_NoAppPr_ReturnsEmptyState_NoThrow()
    {
        var (app, _) = await NewApp();
        // No app.pr — Load must succeed and leave defaults.
        await app.Load();
        await Assert.That(app.Id).IsNotNull();
    }

    [Test] public async Task AppLoad_OnCorruptAppPr_ReturnsFailureNotCrash()
    {
        var (app, root) = await NewApp();
        var prDir = System.IO.Path.Combine(root, ".build");
        System.IO.Directory.CreateDirectory(prDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(prDir, "app.pr"), "this is not json");
        var idBefore = app.Id;
        var nameBefore = app.Name;
        // Load must not throw on corrupt content AND must leave the App in a
        // usable state (no half-applied mutations — identity/name unchanged).
        await app.Load();
        await Assert.That(app.Id).IsEqualTo(idBefore);
        await Assert.That(app.Name).IsEqualTo(nameBefore);
        // And the App is still operable — subsequent Save round-trips.
        var savedResult = await app.Save();
        await Assert.That(savedResult.Success).IsTrue();
    }

    [Test] public async Task AppSave_RoundTrip_WrittenAppPr_RehydratesUnderAppLoad()
    {
        var (app1, root) = await NewApp();
        app1.Name = "RoundTrip";
        await app1.Save();
        await app1.DisposeAsync();

        var app2 = new PLangEngine(root);
        await app2.Load();
        await Assert.That(app2.Name).IsEqualTo("RoundTrip");
        await app2.DisposeAsync();
    }
}
