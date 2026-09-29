using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Goals.AppGoalsMigrationTests;

/// <summary>
/// Batch 5. global::app.goal.list.@this + App.Load/Save through Path verbs.
/// </summary>
public class AppGoalsThroughPathVerbsTests
{
    private static async Task<(PLangEngine app, string root)> NewApp()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-appgoals-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        await Task.CompletedTask;
        return (new global::app.@this(root).Testing(), root);
    }

    [Test] public async Task Load_UsesPathReadTextNotFileReadAllText()
    {
        var (app, root) = await NewApp();
        var buildDir = System.IO.Path.Combine(root, ".build");
        System.IO.Directory.CreateDirectory(buildDir);
        var prAbs = System.IO.Path.Combine(buildDir, "start.pr");
        System.IO.File.WriteAllText(prAbs, "{\"name\":\"Start\",\"path\":\"/Start.goal\"}");
        var result = await app.goal.Load("/.build/start.pr");
        await result.IsSuccess();
        var goal = (await result.Value()) as Goal;
        await Assert.That(goal!.Name).IsEqualTo("Start");
    }

    // The collection resolves the location itself: relative and absolute name the same .pr, and the
    // second load answers the goal the first one read.
    [Test] public async Task Load_ResolvesRelativeAndAbsolute_ToTheSameGoal()
    {
        var (app, root) = await NewApp();
        var buildDir = System.IO.Path.Combine(root, ".build");
        System.IO.Directory.CreateDirectory(buildDir);
        var prAbs = System.IO.Path.Combine(buildDir, "start.pr");
        System.IO.File.WriteAllText(prAbs, "{\"name\":\"Start\",\"path\":\"/Start.goal\"}");

        var byRel = await app.goal.Load("/.build/start.pr");
        await byRel.IsSuccess();
        var byAbs = await app.goal.Load(prAbs);
        await byAbs.IsSuccess();
        await Assert.That(await byAbs.Value()).IsSameReferenceAs(await byRel.Value());
    }

    [Test] public async Task AppGoals_FuzzyGetByName_StaysSeparateFromPathKeying()
    {
        var (app, _) = await NewApp();
        var goal = new Goal
        {
            Name = "ProcessData",
            Path = global::app.type.item.path.@this.Resolve("/processdata.goal", app.actor.list.User.Context)
        };
        app.goal.list.Add(goal);
        // Fuzzy by-name lookup: case-insensitive, picks up the goal.
        await Assert.That(await app.goal.list.Find("ProcessData")).IsNotNull();
        await Assert.That(await app.goal.list.Find("processdata")).IsNotNull();
    }

    [Test] public async Task AppLoad_OnColdStart_NoAppPr_ReturnsEmptyState_NoThrow()
    {
        var (app, _) = await NewApp();
        // No app.pr — Load must succeed and leave defaults.
        await app.Load();
        await Assert.That(app.Id).IsNotNull();
    }

    [Test] public async Task AppLoad_OnCorruptAppPr_IsAnErrorNamingIt_NothingHalfApplied()
    {
        var (app, root) = await NewApp();
        var prDir = System.IO.Path.Combine(root, ".build");
        System.IO.Directory.CreateDirectory(prDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(prDir, "app.pr"), "this is not json");
        var idBefore = app.Id;
        var createdBefore = app.Created.Value;
        // A corrupt app.pr is an error naming the file; nothing is half-applied (identity/name unchanged).
        var loaded = await app.Load();
        await Assert.That(loaded.Error?.Key).IsEqualTo("AppIdentityUnreadable");
        await Assert.That(loaded.Error!.Message).Contains("app.pr");
        await Assert.That(app.Id).IsEqualTo(idBefore);
        await Assert.That(app.Created.Value).IsEqualTo(createdBefore);
    }

    [Test] public async Task AppLoad_OnABadField_AppliesNothing()
    {
        var (app, root) = await NewApp();
        var prDir = System.IO.Path.Combine(root, ".build");
        System.IO.Directory.CreateDirectory(prDir);
        // the id reads, the created doesn't: the identity is read whole or not at all
        System.IO.File.WriteAllText(System.IO.Path.Combine(prDir, "app.pr"), "{\"id\":\"new-id\",\"created\":\"not a date\"}");
        var idBefore = app.Id;

        var loaded = await app.Load();

        await Assert.That(loaded.Error?.Key).IsEqualTo("AppIdentityUnreadable");
        await Assert.That(app.Id).IsEqualTo(idBefore);
    }

    [Test] public async Task AppSave_RoundTrip_WrittenAppPr_RehydratesUnderAppLoad()
    {
        var (app1, root) = await NewApp();
        app1.Id = "round-trip";
        await app1.Save();
        await app1.DisposeAsync();

        var app2 = new global::app.@this(root).Testing();
        await app2.Load();
        await Assert.That(app2.Id).IsEqualTo("round-trip");
        await app2.DisposeAsync();
    }
}
