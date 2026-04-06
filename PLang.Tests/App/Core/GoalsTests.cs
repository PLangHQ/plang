using App;
using global::App.Goals.Goal;
using global::App.FileSystem;
using global::App.FileSystem.Default;

namespace PLang.Tests.App.Core;

public class GoalsTests
{
    [Test]
    public async Task Constructor_StartsEmpty()
    {
        var goals = new EngineGoals();

        await Assert.That(goals.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Add_AddsGoal()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal" };

        goals.Add(goal);

        await Assert.That(goals.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Add_RegistersByName()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal" };

        goals.Add(goal);

        await Assert.That(goals.Get("TestGoal")).IsEqualTo(goal);
    }

    [Test]
    public async Task Add_RegistersByPath()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal", Path = "/goals/test.goal" };

        goals.Add(goal);

        await Assert.That(goals.Get("/goals/test.goal")).IsEqualTo(goal);
    }

    [Test]
    public async Task Get_ByName_ReturnsGoal()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal" };
        goals.Add(goal);

        var result = goals.Get("TestGoal");

        await Assert.That(result).IsEqualTo(goal);
    }

    [Test]
    public async Task Get_CaseInsensitive()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal" };
        goals.Add(goal);

        await Assert.That(goals.Get("testgoal")).IsEqualTo(goal);
        await Assert.That(goals.Get("TESTGOAL")).IsEqualTo(goal);
    }

    [Test]
    public async Task Get_EmptyOrNull_ReturnsNull()
    {
        var goals = new EngineGoals();

        await Assert.That(goals.Get(null!)).IsNull();
        await Assert.That(goals.Get("")).IsNull();
    }

    [Test]
    public async Task Get_NonexistentName_ReturnsNull()
    {
        var goals = new EngineGoals();

        var result = goals.Get("NonexistentGoal");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Get_TriesVariations_WithGoalExtension()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal.goal", Path = "/TestGoal.goal" };
        goals.Add(goal);

        var result = goals.Get("TestGoal");

        await Assert.That(result).IsEqualTo(goal);
    }

    [Test]
    public async Task Get_TriesVariations_WithLeadingSlash()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "/goals/test", Path = "/goals/test" };
        goals.Add(goal);

        var result = goals.Get("/goals/test");

        await Assert.That(result).IsEqualTo(goal);
    }

    [Test]
    public async Task Get_TriesVariations_SlashConversion()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "goals/test", Path = "/goals/test.goal" };
        goals.Add(goal);

        var result = goals.Get("goals\\test");

        await Assert.That(result).IsEqualTo(goal);
    }

    [Test]
    public async Task Indexer_ReturnsGoal()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal" };
        goals.Add(goal);

        var result = goals["TestGoal"];

        await Assert.That(result).IsEqualTo(goal);
    }

    [Test]
    public async Task Indexer_NonexistentName_ReturnsNull()
    {
        var goals = new EngineGoals();

        var result = goals["NonexistentGoal"];

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Contains_ExistingGoal_ReturnsTrue()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "TestGoal", Path = "/TestGoal.goal" });

        await Assert.That(goals.Contains("TestGoal")).IsTrue();
    }

    [Test]
    public async Task Contains_NonexistentGoal_ReturnsFalse()
    {
        var goals = new EngineGoals();

        await Assert.That(goals.Contains("NonexistentGoal")).IsFalse();
    }

    [Test]
    public async Task Remove_RemovesGoal()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "TestGoal", Path = "/TestGoal.goal" });

        var removed = goals.Remove("TestGoal");

        await Assert.That(removed).IsTrue();
        await Assert.That(goals.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Remove_RemovesPathLookups()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "TestGoal", Path = "/TestGoal.goal" });

        goals.Remove("TestGoal");

        await Assert.That(goals.Get("/TestGoal.goal")).IsNull();
    }

    [Test]
    public async Task Remove_NonexistentGoal_ReturnsFalse()
    {
        var goals = new EngineGoals();

        var removed = goals.Remove("NonexistentGoal");

        await Assert.That(removed).IsFalse();
    }

    [Test]
    public async Task Clear_RemovesAllGoals()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "Goal1", Path = "/Goal1.goal" });
        goals.Add(new Goal { Name = "Goal2", Path = "/Goal2.goal" });

        goals.Clear();

        await Assert.That(goals.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Names_ReturnsAllNames()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "Goal1", Path = "/Goal1.goal" });
        goals.Add(new Goal { Name = "Goal2", Path = "/Goal2.goal" });

        var names = goals.Names.ToList();

        await Assert.That(names).Contains("Goal1");
        await Assert.That(names).Contains("Goal2");
    }

    [Test]
    public async Task All_ReturnsAllGoals()
    {
        var goals = new EngineGoals();
        var goal1 = new Goal { Name = "Goal1", Path = "/Goal1.goal" };
        var goal2 = new Goal { Name = "Goal2", Path = "/Goal2.goal" };
        goals.Add(goal1);
        goals.Add(goal2);

        var all = goals.All.ToList();

        await Assert.That(all).Contains(goal1);
        await Assert.That(all).Contains(goal2);
    }

    [Test]
    public async Task Public_ReturnsOnlyPublicGoals()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "PublicGoal", Path = "/PublicGoal.goal", Visibility = Visibility.Public });
        goals.Add(new Goal { Name = "PrivateGoal", Path = "/PrivateGoal.goal", Visibility = Visibility.Private });

        var publicGoals = goals.Public.ToList();

        await Assert.That(publicGoals.Count).IsEqualTo(1);
        await Assert.That(publicGoals[0].Name).IsEqualTo("PublicGoal");
    }

    [Test]
    public async Task Setup_ReturnsOnlySetupGoals()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "SetupGoal", Path = "/SetupGoal.goal", IsSetup = true });
        goals.Add(new Goal { Name = "NormalGoal", Path = "/NormalGoal.goal", IsSetup = false });

        var setupGoals = goals.Setup.Goals.ToList();

        await Assert.That(setupGoals.Count).IsEqualTo(1);
        await Assert.That(setupGoals[0].Name).IsEqualTo("SetupGoal");
    }

    [Test]
    public async Task Events_ReturnsOnlyEventGoals()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "EventGoal", Path = "/EventGoal.goal", IsEvent = true });
        goals.Add(new Goal { Name = "NormalGoal", Path = "/NormalGoal.goal", IsEvent = false });

        var eventGoals = goals.Events.ToList();

        await Assert.That(eventGoals.Count).IsEqualTo(1);
        await Assert.That(eventGoals[0].Name).IsEqualTo("EventGoal");
    }

    [Test]
    public async Task Add_SamePathTwice_ReplacesGoal()
    {
        var goals = new EngineGoals();
        var goal1 = new Goal { Name = "TestGoal", Path = "/TestGoal.goal", Description = "First" };
        var goal2 = new Goal { Name = "TestGoal", Path = "/TestGoal.goal", Description = "Second" };
        goals.Add(goal1);

        goals.Add(goal2);

        await Assert.That(goals.Count).IsEqualTo(1);
        await Assert.That(goals.Get("TestGoal")!.Description).IsEqualTo("Second");
    }

    [Test]
    public async Task Count_ReturnsCorrectCount()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "Goal1", Path = "/Goal1.goal" });
        goals.Add(new Goal { Name = "Goal2", Path = "/Goal2.goal" });
        goals.Add(new Goal { Name = "Goal3", Path = "/Goal3.goal" });

        await Assert.That(goals.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Get_ExcludesSetupGoals()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "SetupDb", Path = "/SetupDb.goal", IsSetup = true });
        goals.Add(new Goal { Name = "NormalGoal", Path = "/NormalGoal.goal", IsSetup = false });

        await Assert.That(goals.Get("SetupDb")).IsNull();
        await Assert.That(goals.Get("NormalGoal")).IsNotNull();
    }

    [Test]
    public async Task GetAsync_ReturnsNull_ForSetupGoalLoadedFromDisk()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-goals-test-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            var fs = new PLangFileSystem(tempDir, "");
            await using var engine = new global::App.@this(fs);

            var buildDir = System.IO.Path.Combine(tempDir, ".build");
            System.IO.Directory.CreateDirectory(buildDir);
            var prPath = System.IO.Path.Combine(buildDir, "setupdb.pr");
            var json = """{"name":"SetupDb","isSetup":true,"path":"/SetupDb.goal","steps":[]}""";
            System.IO.File.WriteAllText(prPath, json);

            var result = await engine.Goals.GetAsync("SetupDb");

            await Assert.That(result).IsNull();
        }
        finally
        {
            System.IO.Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task GetAsync_ReturnsGoal_ForNonSetupGoalLoadedFromDisk()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-goals-test-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            var fs = new PLangFileSystem(tempDir, "");
            await using var engine = new global::App.@this(fs);

            var buildDir = System.IO.Path.Combine(tempDir, ".build");
            System.IO.Directory.CreateDirectory(buildDir);
            var prPath = System.IO.Path.Combine(buildDir, "normalgoal.pr");
            var json = """{"name":"NormalGoal","isSetup":false,"path":"/NormalGoal.goal","steps":[]}""";
            System.IO.File.WriteAllText(prPath, json);

            var result = await engine.Goals.GetAsync("NormalGoal");

            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Name).IsEqualTo("NormalGoal");
        }
        finally
        {
            System.IO.Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task GetByPrPathAsync_ReturnsNull_ForSetupGoal()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-goals-test-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            var fs = new PLangFileSystem(tempDir, "");
            await using var engine = new global::App.@this(fs);

            var buildDir = System.IO.Path.Combine(tempDir, ".build");
            System.IO.Directory.CreateDirectory(buildDir);
            var prPath = System.IO.Path.Combine(buildDir, "setupdb.pr");
            var json = """{"name":"SetupDb","isSetup":true,"path":"/SetupDb.goal","steps":[]}""";
            System.IO.File.WriteAllText(prPath, json);

            var result = await engine.Goals.GetByPrPathAsync(prPath);

            await Assert.That(result).IsNull();
        }
        finally
        {
            System.IO.Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task GetByPrPathAsync_ReturnsNull_ForCachedSetupGoal()
    {
        var goals = new EngineGoals();
        var setupGoal = new Goal { Name = "SetupDb", IsSetup = true, Path = "/SetupDb.goal" };
        goals.Add(setupGoal);

        var result = await goals.GetByPrPathAsync("/.build/setupdb.pr");

        await Assert.That(result).IsNull();
    }

    // --- PrPath keying tests ---
    // PrPath is computed from Path: "/Foo.goal" -> "/.build/foo.pr"

    [Test]
    public async Task Add_KeysByPrPath_PreventsSameNameCollision()
    {
        var goals = new EngineGoals();
        var goal1 = new Goal { Name = "Setup", IsSetup = true, Path = "/Setup.goal" };
        var goal2 = new Goal { Name = "Setup", IsSetup = true, Path = "/Setup/Setup.goal" };

        goals.Add(goal1);
        goals.Add(goal2);

        var setupGoals = goals.Setup.Goals.ToList();
        await Assert.That(setupGoals.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Get_FindsGoalKeyedByPrPath()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "Start", Path = "/Start.goal" };
        goals.Add(goal);

        var found = goals.Get("Start");

        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Name).IsEqualTo("Start");
    }

    [Test]
    public async Task Get_FindsCorrectGoal_WhenMultipleSameNameDifferentPrPath()
    {
        var goals = new EngineGoals();
        var goal1 = new Goal { Name = "Helper", Path = "/a/Helper.goal" };
        var goal2 = new Goal { Name = "Helper", Path = "/b/Helper.goal" };
        goals.Add(goal1);
        goals.Add(goal2);

        var found = goals.Get("Helper");

        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Name).IsEqualTo("Helper");
    }

    [Test]
    public async Task Remove_ByName_WorksWhenKeyedByPrPath()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal" };
        goals.Add(goal);

        var removed = goals.Remove("TestGoal");

        await Assert.That(removed).IsTrue();
        await Assert.That(goals.Get("TestGoal")).IsNull();
        await Assert.That(goals.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Remove_ByName_ClearsPathIndex()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal" };
        goals.Add(goal);

        goals.Remove("TestGoal");

        await Assert.That(goals.Get("/TestGoal.goal")).IsNull();
    }

    [Test]
    public async Task Add_SamePrPath_ReplacesGoal()
    {
        var goals = new EngineGoals();
        var goal1 = new Goal { Name = "Start", Path = "/Start.goal", Description = "First" };
        var goal2 = new Goal { Name = "Start", Path = "/Start.goal", Description = "Second" };

        goals.Add(goal1);
        goals.Add(goal2);

        await Assert.That(goals.Count).IsEqualTo(1);
        await Assert.That(goals.Get("Start")!.Description).IsEqualTo("Second");
    }

    [Test]
    public async Task Add_ThrowsWhenNoPrPath()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal" };

        await Assert.That(() => goals.Add(goal)).ThrowsException();
    }

    [Test]
    public async Task Add_ThrowsWhenPathIsEmptyString()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal", Path = "" };

        await Assert.That(() => goals.Add(goal)).ThrowsException();
    }

    [Test]
    public async Task Names_ExcludesSetupGoals()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "SetupDb", IsSetup = true, Path = "/SetupDb.goal" });
        goals.Add(new Goal { Name = "NormalGoal", IsSetup = false, Path = "/NormalGoal.goal" });

        var names = goals.Names.ToList();

        await Assert.That(names.Count).IsEqualTo(1);
        await Assert.That(names[0]).IsEqualTo("NormalGoal");
    }
}
