using PLangGoal = app.goal.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Goals;

/// <summary>
/// Guards how the goal collection selects the goal a call names, as seen from the calling goal
/// (<see cref="app.goal.list.@this.GetAsync"/>).
///
/// Slash-qualified names (Folder/Leaf) resolve as <c>{folder}/.build/{leaf}.pr</c> — NOT
/// <c>.build/{folder/leaf}.pr</c>. The lookup walks the caller's ancestor folders, then the root.
/// A bare name looks in the caller's own folder, then the root.
///
/// Each case is a real .pr on disk — written by the goal itself — and a caller goal whose Path
/// anchors the walk.
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
        _app = TestApp.Create(_tempDir);
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

    /// <summary>Writes a `.pr` for a goal named <paramref name="goalName"/> at the given path, through
    /// the goal's own writer (the shape the reader reads back).</summary>
    private async Task WritePr(string relativePrPath, string goalName)
    {
        var ctx = _app.User.Context;
        var goal = new PLangGoal { Name = goalName, Path = global::app.type.item.path.@this.Resolve("/" + goalName + ".goal", ctx) };
        var serializer = (global::app.channel.serializer.plang.@this)ctx.Actor!.Channel.Serializers.GetOrDefault("application/plang");
        using var ms = new System.IO.MemoryStream();
        await serializer.SerializeItemAsync(ms, goal, global::app.View.Store);

        var abs = System.IO.Path.Combine(_tempDir, relativePrPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(abs)!);
        await System.IO.File.WriteAllBytesAsync(abs, ms.ToArray());
    }

    /// <summary>A caller goal whose Path anchors the folder walk.</summary>
    private static PLangGoal CallerAt(string callerGoalPath)
        => new() { Name = "Caller", Path = global::app.type.item.path.@this.Resolve(callerGoalPath, global::PLang.Tests.TestApp.SharedContext) };

    [Test]
    public async Task SlashName_Resolved_ByCallerAncestorWalk()
    {
        // Target lives at /system/builder/BuildStep/.build/start.pr. Caller lives in
        // /system/builder/BuildGoal/ — the walk shrinks to /system/builder, where the join hits.
        await WritePr("system/builder/BuildStep/.build/start.pr", "Start");

        var goal = await _app.Goal.GetAsync("BuildStep/Start", CallerAt("/system/builder/BuildGoal/Start.goal"));

        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Name).IsEqualTo("Start");
    }

    [Test]
    public async Task SlashName_Resolved_ByRootRelative_WhenNoAncestorMatches()
    {
        // .pr at /BuildStep/.build/start.pr; no ancestor of /elsewhere contains BuildStep/, so the
        // root is the tier that answers.
        await WritePr("BuildStep/.build/start.pr", "Start");

        var goal = await _app.Goal.GetAsync("BuildStep/Start", CallerAt("/elsewhere/Caller.goal"));

        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Name).IsEqualTo("Start");
    }

    [Test]
    public async Task SlashName_IsTheFolderAndTheName_NeverAnyGoalOfThatName()
    {
        // BuildGoal calls BuildGoal/Start: a Start elsewhere is not it, and neither is BuildGoal itself
        var ctx = _app.User.Context;
        var caller = new PLangGoal { Name = "BuildGoal", Path = global::app.type.item.path.@this.Resolve("/builder/BuildGoal.goal", ctx) };
        _app.Goal.Add(caller);
        _app.Goal.Add(new PLangGoal { Name = "Start", Path = global::app.type.item.path.@this.Resolve("/other/Start.goal", ctx) });

        var none = await _app.Goal.GetAsync("BuildGoal/Start", caller);

        await Assert.That(none).IsNull();

        var start = new PLangGoal { Name = "Start", Path = global::app.type.item.path.@this.Resolve("/builder/BuildGoal/Start.goal", ctx) };
        _app.Goal.Add(start);
        await Assert.That(await _app.Goal.GetAsync("BuildGoal/Start", caller)).IsSameReferenceAs(start);
    }

    [Test]
    public async Task BareName_Resolved_AgainstCallersOwnBuildFolder()
    {
        // .pr at /foo/.build/other.pr — sibling of the caller in /foo/Caller.
        await WritePr("foo/.build/other.pr", "Other");

        var goal = await _app.Goal.GetAsync("Other", CallerAt("/foo/Caller.goal"));

        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Name).IsEqualTo("Other");
    }

    [Test]
    public async Task ChildOfTheCaller_WinsBeforeAnyFile()
    {
        var caller = CallerAt("/foo/Caller.goal");
        var child = new PLangGoal { Name = "Helper", Path = caller.Path, Parent = caller };
        caller.Child.Add(child);

        var goal = await _app.Goal.GetAsync("Helper", caller);

        await Assert.That(goal).IsSameReferenceAs(child);
    }
}
