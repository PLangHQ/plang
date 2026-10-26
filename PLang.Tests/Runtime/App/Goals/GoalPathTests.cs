namespace PLang.Tests.App.Goals;

// A goal read from a .pr is where the .pr is found: its folder is the .pr's folder's parent, its file the name the
// .pr stores. A goal moved with its .build, or a .pr whose older "path" says somewhere else, is where it is.
public class GoalPathTests
{
    private static string TempApp()
        => System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-goalpath-" + Guid.NewGuid().ToString("N")[..8])).FullName;

    // A goal built at /old, its .pr written by the build's own writer, then the folder moved to /new with its .build.
    private static async Task<string> BuiltThenMoved(string dir, params global::PLang.Tests.Shared.Make.StepDef[] steps)
    {
        await using (var builder = new global::app.@this(dir, global::PLang.Tests.Parent.Actor).Testing())
        {
            var ctx = builder.actor.list.System.Context;
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "old"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "old", "Start.goal"), "Start\n");
            var goal = global::PLang.Tests.Shared.Make.Goal(ctx, "Start", "/old/Start.goal", steps);
            await (await goal.Pr!.Path.Save(ctx.Ok(goal), ctx)).IsSuccess();
        }
        System.IO.Directory.Move(System.IO.Path.Combine(dir, "old"), System.IO.Path.Combine(dir, "new"));
        return dir;
    }

    [Test]
    public async Task AGoalMovedWithItsBuild_LoadsAtItsNewPath_AndLoadsAgainByIt()
    {
        var dir = await BuiltThenMoved(TempApp());
        try
        {
            await using var app = new global::app.@this(dir, global::PLang.Tests.Parent.Actor).Testing();

            var loaded = await app.goal.Load("/new/Start.goal");
            var goal = (global::app.goal.@this)(await loaded.Value())!;

            await Assert.That(goal.Path!.ToString()).IsEqualTo("/new/Start.goal");
            // a test run loads its goal again by the goal's own path
            await (await app.goal.Load(goal.Path.ToString())).IsSuccess();
            await (await goal.Start(app.actor.list.User.Context)).IsSuccess();
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [Test]
    public async Task AMovedGoal_IsFoundHeld_OnItsSecondLoad()
    {
        var dir = await BuiltThenMoved(TempApp());
        try
        {
            await using var app = new global::app.@this(dir, global::PLang.Tests.Parent.Actor).Testing();

            var first = await (await app.goal.Load("/new/Start.goal")).Value();
            var second = await (await app.goal.Load("/new/Start.goal")).Value();

            await Assert.That(second).IsSameReferenceAs(first);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [Test]
    public async Task AMovedGoal_ReadsARelativeFile_InItsOwnFolder()
    {
        var dir = TempApp();
        await using (var maker = new global::app.@this(dir, global::PLang.Tests.Parent.Actor).Testing())
        {
            var ctx = maker.actor.list.System.Context;
            var read = global::PLang.Tests.Shared.Make.Action(ctx, "file", "read", ("Path", "note.txt"));
            var keep = global::PLang.Tests.Shared.Make.Action(ctx, "variable", "set",
                global::PLang.Tests.Shared.Make.Param(ctx, "Name", "note", "variable"), ("Value", "%!data%"));
            await BuiltThenMoved(dir, global::PLang.Tests.Shared.Make.Step("read note.txt, write to %note%", read, keep));
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "new", "note.txt"), "where it is");
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "old"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "old", "note.txt"), "where it was");
        try
        {
            await using var app = new global::app.@this(dir, global::PLang.Tests.Parent.Actor).Testing();
            var user = app.actor.list.User.Context;
            var goal = (global::app.goal.@this)(await (await app.goal.Load("/new/Start.goal")).Value())!;

            await (await goal.Start(user)).IsSuccess();

            await Assert.That((await (await user.Variable.Get("note")).Value())?.ToString()).IsEqualTo("where it is");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // An app's goal never carries /system/: a .pr in the app whose older "path" says /system/… is the app's.
    [Test]
    public async Task AnAppsPr_WhoseOldPathSaysSystem_IsTheAppsGoal_NotTheOs()
    {
        var dir = TempApp();
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "mine", ".build"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "mine", "Show.goal"), "Show\n");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "mine", ".build", "show.pr"),
                """{"name":"Show","step":[],"path":"/system/error/Show.goal","prPath":"/system/error/.build/show.pr"}""");
            await using var app = new global::app.@this(dir, global::PLang.Tests.Parent.Actor).Testing();
            var ctx = app.actor.list.User.Context;

            var goal = (global::app.goal.@this)(await (await app.goal.Load("/mine/Show.goal")).Value())!;
            bool trusted;
            await using (ctx.call.Push(goal, await ctx.call.Setting())) trusted = global::app.type.item.path.@this.AskedByOs(ctx);

            await Assert.That(goal.Path!.ToString()).IsEqualTo("/mine/Show.goal");
            await Assert.That(goal.IsSystem).IsFalse();
            await Assert.That(trusted).IsFalse();
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // A .pr's "file" is only the goal file's NAME (decision 745, security F1): one that names a PATH — a
    // separator, a `..` segment, a rooted or drive form — is refused before it is combined, so a .pr shipped
    // with someone else's app can never aim its goal's folder elsewhere (under the os root, which AskedByOs
    // trusts). Refused loudly, not reduced to a last segment.
    [Test]
    [Arguments("../evil")]
    [Arguments("/evil")]
    [Arguments("sub/evil")]
    [Arguments("..\\evil")]
    public async Task APrFile_ThatNamesAPath_IsRefused(string file)
    {
        var dir = TempApp();
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "mine", ".build"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "mine", "Show.goal"), "Show\n");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "mine", ".build", "show.pr"),
                $$"""{"name":"Show","step":[],"file":{{System.Text.Json.JsonSerializer.Serialize(file)}}}""");
            await using var app = new global::app.@this(dir, global::PLang.Tests.Parent.Actor).Testing();

            var refused = false;
            string? message = null;
            try
            {
                var loaded = await app.goal.Load("/mine/Show.goal");
                if (!loaded.Success) { refused = true; message = loaded.Error!.Message; }
                else { await loaded.Value(); }
            }
            catch (global::app.error.AppException ex) { refused = true; message = ex.Error.Message; }

            await Assert.That(refused).IsTrue().Because($"a .pr 'file' of '{file}' must be refused");
            await Assert.That(message!).Contains("bare");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // A .pr written before a goal stored only its file name: its "path" and "prPath" read to the same goal.
    [Test]
    public async Task AnOlderPr_WithPathAndPrPath_ReadsToTheSameGoal()
    {
        var dir = TempApp();
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "a", ".build"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "a", "Start.goal"), "Start\n");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "a", ".build", "start.pr"),
                """{"name":"Start","comment":"c","step":[],"child":[{"name":"Show","step":[],"path":"/a/Start.goal","prPath":"/a/.build/start.pr"}],"path":"/a/Start.goal","prPath":"/a/.build/start.pr"}""");
            await using var app = new global::app.@this(dir, global::PLang.Tests.Parent.Actor).Testing();

            var goal = (global::app.goal.@this)(await (await app.goal.Load("/a/Start.goal")).Value())!;

            await Assert.That(goal.Name).IsEqualTo("Start");
            await Assert.That(goal.Comment).IsEqualTo("c");
            await Assert.That(goal.Path!.ToString()).IsEqualTo("/a/Start.goal");
            await Assert.That(goal.Child.Items().Single().Path!.ToString()).IsEqualTo("/a/Start.goal");
            await Assert.That(goal.Child.Items().Single().Address).IsEqualTo("/a/Start#Show");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // The os's system folder and what is under it read as /system; a folder that only starts with its name doesn't.
    [Test]
    public async Task TheOsSystemFolder_AndWhatIsUnderIt_ReadAsSystem()
    {
        await using var app = new global::app.@this(TempApp(), global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.System.Context;
        var system = System.IO.Path.Combine(app.filesystem.os.Absolute, "system");

        await Assert.That(global::app.type.item.path.@this.Resolve(system, ctx).Raw).IsEqualTo("/system");
        await Assert.That(global::app.type.item.path.@this.Resolve(System.IO.Path.Combine(system, "error", "Show.goal"), ctx).Raw)
            .IsEqualTo("/system/error/Show.goal");
        await Assert.That(global::app.type.item.path.@this.Resolve(system + "foo", ctx).Raw).DoesNotStartWith("/system");
    }

    // An os goal the list's walk reaches is /system/…: a system goal, wherever the os folder is.
    [Test]
    public async Task AnOsGoalTheWalkReaches_IsUnderSystem()
    {
        var dir = TempApp();
        try
        {
            await using var app = new global::app.@this(dir, global::PLang.Tests.Parent.Actor).Testing();
            var ctx = app.actor.list.System.Context;

            var shown = await app.goal.list.Walk(null, ctx).FirstAsync(g => g.Name == "Show" && g.Path!.ToString()!.Contains("error"));

            await Assert.That(shown.Path!.ToString()).IsEqualTo("/system/error/Show.goal");
            await Assert.That(shown.IsSystem).IsTrue();
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
