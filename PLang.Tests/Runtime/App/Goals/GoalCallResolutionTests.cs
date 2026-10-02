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
        _app = new global::app.@this(_tempDir).Testing();
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
    /// the goal's own writer (the shape the reader reads back), and its `.goal` beside the `.build` folder.</summary>
    private async Task WritePr(string relativePrPath, string goalName)
    {
        var ctx = _app.actor.list.User.Context;
        var abs = System.IO.Path.Combine(_tempDir, relativePrPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var source = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(abs)!)!, goalName + ".goal");
        var relative = "/" + System.IO.Path.GetRelativePath(_tempDir, source).Replace('\\', '/');
        var goal = new PLangGoal { Name = goalName, Path = global::app.type.item.path.@this.Resolve(relative, ctx) };
        var pr = await ctx.Pr(goal);

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(abs)!);
        await System.IO.File.WriteAllTextAsync(abs, pr);
        await System.IO.File.WriteAllTextAsync(source, goalName + "\n");
    }

    /// <summary>A caller goal whose Path anchors the folder walk.</summary>
    private PLangGoal CallerAt(string callerGoalPath)
        => new() { Name = "Caller", Path = global::app.type.item.path.@this.Resolve(callerGoalPath, _app.actor.list.User.Context) };

    // A goal's name in a goal slot (a call's Name) selects the goal that exists — here one only on disk, loaded by
    // the selection — and selecting is no birth: nothing bound on goal create fires. %!app.goal["…"]% selects the
    // same goal the same way. A goal read from its .pr is a birth.
    [Test]
    public async Task AGoalSlot_SelectsTheGoalOnDisk_FiresNoCreate_AndTheAppSelectsTheSame()
    {
        await WritePr(".build/target.pr", "Target");
        var ctx = _app.actor.list.User.Context;
        var births = new List<string>();
        _app.type.list["goal"].Own().Bind("create", global::app.@event.When.after,
            (_, data, c) => { births.Add(data.Type.Name); return Task.FromResult(data); },
            _app.actor.list.User, global::app.@event.binding.Scope.actor);

        var slot = new global::app.data.@this("", new global::app.type.item.text.@this("Target"), context: ctx).As<PLangGoal>();
        var selected = await slot.Value();
        var byApp = await new global::app.type.item.variable.@this("!app.goal[\"Target\"]").Start(ctx);

        await Assert.That(selected?.Name).IsEqualTo("Target");
        await Assert.That(births).IsEmpty();
        await Assert.That(byApp.Peek()).IsSameReferenceAs(selected);

        // the binding is live: a goal born from its .pr fires it
        var pr = await ctx.Pr(new PLangGoal { Name = "Born", Path = global::app.type.item.path.@this.Resolve("/Born.goal", ctx) });
        await _app.type.list["goal"].Create(System.Text.Encoding.UTF8.GetBytes(pr), ctx);
        await Assert.That(births.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SlashName_Resolved_ByCallerAncestorWalk()
    {
        // Target lives at /system/builder/BuildStep/.build/start.pr. Caller lives in
        // /system/builder/BuildGoal/ — the walk shrinks to /system/builder, where the join hits.
        await WritePr("system/builder/BuildStep/.build/start.pr", "Start");

        var goal = await _app.goal.list.Find("BuildStep/Start", CallerAt("/system/builder/BuildGoal/Start.goal")).Found();

        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Name).IsEqualTo("Start");
    }

    [Test]
    public async Task SlashName_Resolved_ByRootRelative_WhenNoAncestorMatches()
    {
        // .pr at /BuildStep/.build/start.pr; no ancestor of /elsewhere contains BuildStep/, so the
        // root is the tier that answers.
        await WritePr("BuildStep/.build/start.pr", "Start");

        var goal = await _app.goal.list.Find("BuildStep/Start", CallerAt("/elsewhere/Caller.goal")).Found();

        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Name).IsEqualTo("Start");
    }

    [Test]
    public async Task SlashName_IsTheFolderAndTheName_NeverAnyGoalOfThatName()
    {
        // BuildGoal calls BuildGoal/Start: a Start elsewhere is not it, and neither is BuildGoal itself
        var ctx = _app.actor.list.User.Context;
        var caller = new PLangGoal { Name = "BuildGoal", Path = global::app.type.item.path.@this.Resolve("/builder/BuildGoal.goal", ctx) };
        _app.goal.list.Add(caller);
        _app.goal.list.Add(new PLangGoal { Name = "Start", Path = global::app.type.item.path.@this.Resolve("/other/Start.goal", ctx) });

        var none = await _app.goal.list.Find("BuildGoal/Start", caller).Found();

        await Assert.That(none).IsNull();

        var start = new PLangGoal { Name = "Start", Path = global::app.type.item.path.@this.Resolve("/builder/BuildGoal/Start.goal", ctx) };
        _app.goal.list.Add(start);
        await Assert.That(await _app.goal.list.Find("BuildGoal/Start", caller).Found()).IsSameReferenceAs(start);
    }

    [Test]
    public async Task BareName_Resolved_AgainstCallersOwnBuildFolder()
    {
        // .pr at /foo/.build/other.pr — sibling of the caller in /foo/Caller.
        await WritePr("foo/.build/other.pr", "Other");

        var goal = await _app.goal.list.Find("Other", CallerAt("/foo/Caller.goal")).Found();

        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Name).IsEqualTo("Other");
    }

    // an app's own /system/ goal is found before the os's of the same name
    [Test]
    public async Task TheAppsSystemGoal_BeatsTheOs()
    {
        await WritePr("system/error/.build/show.pr", "show");

        var goal = await _app.goal.list.Find("/system/error/show").Found();

        await Assert.That(goal?.Path?.ToString()).IsEqualTo("/system/error/show.goal");
        await Assert.That(goal!.Step.Count).IsEqualTo(0);
    }

    // an os goal calling a goal beside it finds the app's own copy first: the os's builder, calling BuildGoal,
    // gets the app's /system/builder/BuildGoal.goal
    [Test]
    public async Task AnOsGoal_CallingAGoalBesideIt_FindsTheAppsCopy()
    {
        await WritePr("system/builder/.build/buildgoal.pr", "BuildGoal");
        var caller = CallerAt(_app.OsAbsolutePath + "/system/builder/Build.goal");

        var goal = await _app.goal.list.Find("BuildGoal", caller).Found();

        await Assert.That(goal?.Path?.ToString()).IsEqualTo("/system/builder/BuildGoal.goal");
        await Assert.That(goal!.Step.Count).IsEqualTo(0);
    }

    // a goal in a subfolder is read from the .build beside it
    [Test]
    public async Task ASubfolderGoal_ReadsTheBuildBesideIt()
    {
        await WritePr("sub/.build/inner.pr", "Inner");

        var loaded = await _app.goal.Load("/sub/Inner.goal");

        await loaded.IsSuccess();
        await Assert.That((await loaded.Value() as PLangGoal)?.Name).IsEqualTo("Inner");
        await Assert.That(PLangGoal.Pr(global::app.type.item.path.@this.Resolve("/sub/Inner.goal", _app.actor.list.User.Context)).ToString())
            .IsEqualTo("/sub/.build/inner.pr");
    }

    // a .goal with no .pr: the call says it isn't built, not that it isn't there
    [Test]
    public async Task AGoalNotBuilt_AnswersGoalNotBuilt()
    {
        System.IO.File.WriteAllText(System.IO.Path.Combine(_tempDir, "Fresh.goal"), "Fresh\n- write out 1\n");

        var found = await _app.goal.list.Find("Fresh");
        var slot = new global::app.data.@this("", new global::app.type.item.text.@this("Fresh"), context: _app.actor.list.User.Context)
            .As<PLangGoal>();
        var selected = await slot.Value();

        await found.IsFailure();
        await Assert.That(found.Error!.Key).IsEqualTo("GoalNotBuilt");
        await Assert.That(found.Error.Message).Contains("run plang build");
        await Assert.That(selected).IsNull();
        await Assert.That(slot.Error?.Key).IsEqualTo("GoalNotBuilt");
    }

    [Test]
    public async Task ChildOfTheCaller_WinsBeforeAnyFile()
    {
        var caller = CallerAt("/foo/Caller.goal");
        var child = new PLangGoal { Name = "Helper", Path = caller.Path, Parent = caller };
        caller.Child.Add(child);

        var goal = await _app.goal.list.Find("Helper", caller).Found();

        await Assert.That(goal).IsSameReferenceAs(child);
    }
}
