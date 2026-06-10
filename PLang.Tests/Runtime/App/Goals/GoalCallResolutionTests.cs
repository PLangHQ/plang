using System.Text.Json;
using app.goal;
using app.goal.steps;
using app.goal.steps.step;
using app.goal.steps.step.actions;
using PLangAction = app.goal.steps.step.actions.action.@this;
using PLangGoal = app.goal.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Goals;

/// <summary>
/// Guards the four-tier slash-qualified resolution in
/// <see cref="app.goal.GoalCall.GetGoalAsync"/>.
///
/// Slash-qualified Names (Folder/Leaf) resolve as
/// <c>{folder}/.build/{leaf}.pr</c> — NOT <c>.build/{folder/leaf}.pr</c>.
/// The resolver walks the caller's ancestor folders, then root, then
/// context-relative. <see cref="GoalCall.LoadFromFile"/> leaf-matches a
/// slash-qualified Name against the loaded goal's unqualified Name so the
/// match doesn't false-fail just because of the folder prefix.
///
/// Each tier here is a real .pr on disk + an Action wired through a Step
/// to a caller Goal whose Path determines the ancestor anchor.
/// </summary>
public class GoalCallResolutionTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_goalcall_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public async Task Teardown()
    {
        try
        {
            await _app.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort */ }
    }

    /// <summary>Write a `.pr` for a goal with the given Name at the given on-disk path.</summary>
    private void WritePr(string relativePrPath, string goalName)
    {
        var abs = System.IO.Path.Combine(_tempDir, relativePrPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(abs)!);
        var goal = new PLangGoal { Name = goalName, Path = "/" + goalName, Steps = new GoalSteps() };
        _ = goal.Hash; // ensures serializable shape is snapshotted
        System.IO.File.WriteAllText(abs, JsonSerializer.Serialize(goal, global::app.Utils.Json.CamelCaseIndented));
    }

    /// <summary>
    /// Builds an Action whose Step.Goal.Path = <paramref name="callerGoalPath"/> so
    /// GoalCall's caller-ancestor walk starts from that folder.
    /// </summary>
    private PLangAction CallerAt(string callerGoalPath)
    {
        var goal = new PLangGoal { Name = "Caller", Path = callerGoalPath, Steps = new GoalSteps() };
        var step = new Step { Index = 0, Text = "call something", Goal = goal };
        goal.Steps.Add(step);
        var action = new PLangAction { Module = "goal", ActionName = "call", Step = step };
        step.Actions.Add(action);
        return action;
    }

    // --- slash name resolved via the caller-ancestor walk -------------

    [Test]
    public async Task SlashName_Resolved_ByCallerAncestorWalk()
    {
        // Target lives at /system/builder/BuildStep/.build/start.pr
        // Caller lives in /system/builder/BuildGoal/ — the walk shrinks
        // /system/builder/BuildGoal → /system/builder, where the join hits.
        WritePr("system/builder/BuildStep/.build/start.pr", "Start");
        var caller = CallerAt("/system/builder/BuildGoal/Start");

        var call = new GoalCall { Name = "BuildStep/Start", Action = caller };
        var result = await call.GetGoalAsync(_app, _app.User.Context);

        await result.IsSuccess();
        await Assert.That((await result.Value()) is PLangGoal).IsTrue();
        await Assert.That(((PLangGoal)(await result.Value())!).Name).IsEqualTo("Start");
    }

    // --- slash name resolved via root-relative (no matching ancestor) -

    [Test]
    public async Task SlashName_Resolved_ByRootRelative_WhenNoAncestorMatches()
    {
        // .pr at /BuildStep/.build/start.pr (project-root level).
        // Caller in /elsewhere/Caller — no ancestor of /elsewhere contains
        // BuildStep/. The walk misses; root-relative is the next tier.
        WritePr("BuildStep/.build/start.pr", "Start");
        var caller = CallerAt("/elsewhere/Caller");

        var call = new GoalCall { Name = "BuildStep/Start", Action = caller };
        var result = await call.GetGoalAsync(_app, _app.User.Context);

        await result.IsSuccess();
        await Assert.That((await result.Value()) is PLangGoal).IsTrue();
        await Assert.That(((PLangGoal)(await result.Value())!).Name).IsEqualTo("Start");
    }

    // --- bare name unchanged from prior behaviour (regression guard) --

    [Test]
    public async Task BareName_Resolved_AgainstCallersOwnBuildFolder()
    {
        // .pr at /foo/.build/other.pr — sibling of the caller in /foo/Caller.
        WritePr("foo/.build/other.pr", "Other");
        var caller = CallerAt("/foo/Caller");

        var call = new GoalCall { Name = "Other", Action = caller };
        var result = await call.GetGoalAsync(_app, _app.User.Context);

        await result.IsSuccess();
        await Assert.That((await result.Value()) is PLangGoal).IsTrue();
        await Assert.That(((PLangGoal)(await result.Value())!).Name).IsEqualTo("Other");
    }

    // --- LoadFromFile leaf-matches a slash-qualified Name -------------

    [Test]
    public async Task LoadFromFile_SlashName_LeafMatchesAgainstUnqualifiedGoalName()
    {
        // Pre-resolved PrPath is authoritative: GetGoalAsync hits LoadFromFile
        // directly. The saved goal's own Name is just "Start"; the GoalCall's
        // Name is "BuildGoal/Start" (folder-qualified). Leaf-match resolves
        // "BuildGoal/Start" → "Start" so the goal returns instead of failing
        // with GoalNotFound — the LoadFromFile side of leaf-matching.
        WritePr("system/builder/BuildGoal/.build/start.pr", "Start");
        var caller = CallerAt("/anywhere/Other");

        var call = new GoalCall
        {
            Name = "BuildGoal/Start",
            PrPath = "/system/builder/BuildGoal/.build/start.pr",
            Action = caller
        };
        var result = await call.GetGoalAsync(_app, _app.User.Context);

        await result.IsSuccess();
        await Assert.That((await result.Value()) is PLangGoal).IsTrue();
        await Assert.That(((PLangGoal)(await result.Value())!).Name).IsEqualTo("Start");
    }
}
