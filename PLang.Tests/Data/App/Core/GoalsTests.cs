using app;
using app.goal;
using app.type.item.path;

namespace PLang.Tests.App.Core;

// The goals: app.goal is the type over the goals read so far (goal.list, a list<goal>). A goal is picked
// by its address through the type; a call's name is found by the list, from where it is called.
public class GoalsTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.goal.list.@this Goals() => new(app.actor.list.User.Context.App);

    // a goal at path — a setup goal is one by its place (/setup/) or its name (Setup)
    private Goal Named(string name, string path, string? comment = null)
        => new() { Name = name, Path = global::app.type.item.path.@this.Resolve(path, app.actor.list.User.Context), Comment = comment };

    private static string TempApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-goals-test-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, ".build"));
        return dir;
    }

    // ---- the list ----

    [Test]
    public async Task Constructor_StartsEmpty()
    {
        await Assert.That(Goals().CountRaw).IsEqualTo(0);
    }

    [Test]
    public async Task Add_HoldsTheGoal()
    {
        var goals = Goals();
        var goal = Named("TestGoal", "/TestGoal.goal");

        goals.Add(goal);

        await Assert.That(goals.Items().Single()).IsEqualTo(goal);
    }

    [Test]
    public async Task Add_SamePrPath_ReplacesGoal()
    {
        var goals = Goals();
        goals.Add(Named("Start", "/Start.goal", comment: "First"));
        goals.Add(Named("Start", "/Start.goal", comment: "Second"));

        await Assert.That(goals.CountRaw).IsEqualTo(1);
        await Assert.That((await goals.Find("Start").Found())!.Comment).IsEqualTo("Second");
    }

    [Test]
    public async Task Add_KeysByPrPath_SameNameInTwoFiles_BothHeld()
    {
        var goals = Goals();
        goals.Add(Named("Setup", "/Setup.goal"));
        goals.Add(Named("Setup", "/Setup/Setup.goal"));

        await Assert.That(goals.Setup.Goals.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Add_ThrowsWhenNoPrPath()
    {
        await Assert.That(() => Goals().Add(new Goal { Name = "TestGoal" })).ThrowsException();
    }

    [Test]
    public async Task Add_ThrowsWhenPathIsEmptyString()
    {
        await Assert.That(() => Goals().Add(Named("TestGoal", ""))).ThrowsException();
    }

    [Test]
    public async Task Setup_ReturnsOnlySetupGoals()
    {
        var goals = Goals();
        goals.Add(Named("SetupGoal", "/setup/SetupGoal.goal"));
        goals.Add(Named("NormalGoal", "/NormalGoal.goal"));

        await Assert.That(goals.Setup.Goals.Select(g => g.Name).ToList()).IsEquivalentTo(new[] { "SetupGoal" });
    }

    // ---- Find: the goal a call names ----

    [Test]
    public async Task Find_ByName_AnyCase()
    {
        var goals = Goals();
        var goal = Named("TestGoal", "/TestGoal.goal");
        goals.Add(goal);

        await Assert.That(await goals.Find("TestGoal").Found()).IsEqualTo(goal);
        await Assert.That(await goals.Find("testgoal").Found()).IsEqualTo(goal);
    }

    [Test]
    public async Task Find_EmptyOrUnknown_IsNull()
    {
        var goals = Goals();

        await Assert.That(await goals.Find("").Found()).IsNull();
        await Assert.That(await goals.Find("NoSuchGoalAnywhere").Found()).IsNull();
    }

    [Test]
    public async Task Find_ByPath_InTheFormsACallWrites()
    {
        var goals = Goals();
        var goal = Named("test", "/goals/test.goal");
        goals.Add(goal);

        await Assert.That(await goals.Find("/goals/test.goal").Found()).IsEqualTo(goal);
        await Assert.That(await goals.Find("goals/test").Found()).IsEqualTo(goal);
        await Assert.That(await goals.Find("goals\\test").Found()).IsEqualTo(goal);
    }

    [Test]
    public async Task Find_NeverASetupGoal()
    {
        var goals = Goals();
        goals.Add(Named("SetupDb", "/setup/SetupDb.goal"));
        goals.Add(Named("NormalGoal", "/NormalGoal.goal"));

        await Assert.That(await goals.Find("SetupDb").Found()).IsNull();
        await Assert.That(await goals.Find("NormalGoal").Found()).IsNotNull();
    }

    [Test]
    public async Task Find_SameNameInTwoFolders_TheLastRead()
    {
        var goals = Goals();
        goals.Add(Named("Helper", "/a/Helper.goal"));
        var later = Named("Helper", "/b/Helper.goal");
        goals.Add(later);

        await Assert.That(await goals.Find("Helper").Found()).IsEqualTo(later);
        await Assert.That((await goals.Find("a/Helper").Found())!.Path!.ToString()).IsEqualTo("/a/Helper.goal");
    }

    [Test]
    public async Task Find_ReadsAGoalFromDisk_NeverASetupGoal()
    {
        var dir = TempApp();
        try
        {
            await using var engine = new global::app.@this(dir).Testing();
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, ".build", "normalgoal.pr"),
                """{"name":"NormalGoal","isSetup":false,"path":"/NormalGoal.goal","step":[]}""");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, ".build", "setup.pr"),
                """{"name":"Setup","path":"/Setup.goal","step":[]}""");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "NormalGoal.goal"), "NormalGoal\n");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "Setup.goal"), "Setup\n");

            await Assert.That((await engine.goal.list.Find("NormalGoal").Found())!.Name).IsEqualTo("NormalGoal");
            await Assert.That(await engine.goal.list.Find("Setup").Found()).IsNull();
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ---- Load ----

    [Test]
    public async Task Load_RefusesASetupGoal()
    {
        var dir = TempApp();
        try
        {
            await using var engine = new global::app.@this(dir).Testing();
            var pr = System.IO.Path.Combine(dir, ".build", "setup.pr");
            System.IO.File.WriteAllText(pr, """{"name":"Setup","path":"/Setup.goal","step":[]}""");

            var result = await engine.goal.Load(System.IO.Path.Combine(dir, "Setup.goal"));

            await Assert.That(result.Error?.Key).IsEqualTo("SetupGoal");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [Test]
    public async Task Load_RefusesAHeldSetupGoal()
    {
        // rooted where Named's paths resolve, so the held goal's .pr is the one loaded
        await using var app = new global::app.@this(this.app.AbsolutePath).Testing();
        app.goal.list.Add(Named("SetupDb", "/setup/SetupDb.goal"));

        var result = await app.goal.Load("/setup/SetupDb.goal");

        await Assert.That(result.Error?.Key).IsEqualTo("SetupGoal");
    }

    // ---- one goal: parent, address, visibility ----

    [Test]
    public async Task SubGoal_KnowsItsParent_ItsAddressAndItsVisibility()
    {
        var context = app.actor.list.User.Context;
        var path = global::app.type.item.path.@this.Resolve("/Start.goal", context);
        var start = Goal.Parse("Start\n- write out 'a'\n\nShow\n- write out 'b'\n", path, context)!;

        var show = start.Child.Items().Single();

        await Assert.That(show.Parent).IsEqualTo(start);
        await Assert.That(start.Address).IsEqualTo("/Start");
        await Assert.That(show.Address).IsEqualTo("/Start#Show");
        await Assert.That(Equals(start.Visibility.Value, Visibility.Public)).IsTrue();
        await Assert.That(Equals(show.Visibility.Value, Visibility.Private)).IsTrue();
    }

    [Test]
    public async Task Match_TheGoalOrOneOfItsSubGoals_ByAddress()
    {
        var context = app.actor.list.User.Context;
        var start = Goal.Parse("Start\n- write out 'a'\n\nShow\n- write out 'b'\n",
            global::app.type.item.path.@this.Resolve("/Start.goal", context), context)!;

        await Assert.That(await start.Match("/start")).IsEqualTo(start);
        await Assert.That((await start.Match("/start#show"))!.Name).IsEqualTo("Show");
        await Assert.That(await start.Match("/other")).IsNull();
    }

    // ---- the type: Get by address, all ----

    [Test]
    public async Task TypeGet_ByAddress_AHeldGoal_WithoutReading()
    {
        var dir = TempApp();
        try
        {
            await using var engine = new global::app.@this(dir).Testing();
            var goal = Named("Helper", "/a/Helper.goal");
            engine.goal.list.Add(goal);

            var found = await engine.goal.Get("/a/helper");

            await found.IsSuccess();
            await Assert.That(await found.Value()).IsEqualTo(goal);
            await Assert.That((await engine.goal.Get("/no/such")).Error!.Key).IsEqualTo("NotFound");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [Test]
    public async Task All_ListsTheAppsGoals_PublicByDefault_PrivateWhenAsked()
    {
        var dir = TempApp();
        try
        {
            await using var engine = new global::app.@this(dir).Testing();
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, ".build", "start.pr"),
                """{"name":"Start","path":"/Start.goal","step":[],"child":[{"name":"Show","path":"/Start.goal","step":[]}]}""");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "Start.goal"), "Start\n");
            // a goal not built, and one in a dot-folder, are no goals of the app
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "Draft.goal"), "Draft\n");
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, ".bot"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, ".bot", "Old.goal"), "Old\n");
            var context = engine.actor.list.User.Context;

            var appOnly = new global::app.type.item.dict.@this();
            appOnly.Set("os", false);
            var every = await engine.goal.list.All(context, appOnly);
            await Assert.That(((global::app.type.item.list.@this<Goal>)every).Items().Select(g => g.Name).ToList())
                .IsEquivalentTo(new[] { "Start" });

            var both = new global::app.type.item.dict.@this();
            both.Set("os", false);
            both.Set("visibility", new global::app.type.item.list.@this(new global::app.type.item.@this[]
                { new global::app.type.item.text.@this("public"), new global::app.type.item.text.@this("private") }));
            var all = await engine.goal.list.All(context, both);
            await Assert.That(((global::app.type.item.list.@this<Goal>)all).Items().Select(g => g.Name).ToList())
                .IsEquivalentTo(new[] { "Start", "Show" });

            // the default lists the system's too (none beside a test binary) and the app's
            var byDefault = (global::app.type.item.list.@this<Goal>)await engine.goal.list.All(context);
            await Assert.That(byDefault.Items().Any(g => g.Name == "Start")).IsTrue();
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [Test]
    public async Task AllWord_OnAList_IsEveryItem_EvenOnAnEmptyList()
    {
        var context = app.actor.list.User.Context;
        var empty = new global::app.data.@this("l", new global::app.type.item.list.@this(), context: context);

        var all = await empty.Get("all");

        await Assert.That(all.IsInitialized).IsTrue();
        await Assert.That(all.Peek()).IsTypeOf<global::app.type.item.list.@this>();
    }
}
