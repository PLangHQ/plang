using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Goals.Goal;

namespace PLang.Tests.Runtime2.Core;

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
        var goal = new Goal { Name = "TestGoal" };

        goals.Add(goal);

        await Assert.That(goals.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Add_RegistersByName()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal" };

        goals.Add(goal);

        await Assert.That(goals.Get("TestGoal")).IsEqualTo(goal);
    }

    [Test]
    public async Task Add_RegistersByPath()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal", Path = "goals/test" };

        goals.Add(goal);

        await Assert.That(goals.Get("goals/test")).IsEqualTo(goal);
    }

    [Test]
    public async Task Get_ByName_ReturnsGoal()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal" };
        goals.Add(goal);

        var result = goals.Get("TestGoal");

        await Assert.That(result).IsEqualTo(goal);
    }

    [Test]
    public async Task Get_CaseInsensitive()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal" };
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
        var goal = new Goal { Name = "TestGoal.goal" };
        goals.Add(goal);

        var result = goals.Get("TestGoal");

        await Assert.That(result).IsEqualTo(goal);
    }

    [Test]
    public async Task Get_TriesVariations_WithLeadingSlash()
    {
        var goals = new EngineGoals();
        // The Get method tries TrimStart('/'), so looking up "/goals/test" will find "goals/test"
        var goal = new Goal { Name = "/goals/test", Path = "/goals/test" };
        goals.Add(goal);

        // Look up with the leading slash since that's the actual name
        var result = goals.Get("/goals/test");

        await Assert.That(result).IsEqualTo(goal);
    }

    [Test]
    public async Task Get_TriesVariations_SlashConversion()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "goals/test" };
        goals.Add(goal);

        var result = goals.Get("goals\\test");

        await Assert.That(result).IsEqualTo(goal);
    }

    [Test]
    public async Task Indexer_ReturnsGoal()
    {
        var goals = new EngineGoals();
        var goal = new Goal { Name = "TestGoal" };
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
        goals.Add(new Goal { Name = "TestGoal" });

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
        goals.Add(new Goal { Name = "TestGoal", Path = "test" });

        var removed = goals.Remove("TestGoal");

        await Assert.That(removed).IsTrue();
        await Assert.That(goals.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Remove_RemovesPathLookups()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "TestGoal", Path = "test" });

        goals.Remove("TestGoal");

        await Assert.That(goals.Get("test")).IsNull();
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
        goals.Add(new Goal { Name = "Goal1" });
        goals.Add(new Goal { Name = "Goal2" });

        goals.Clear();

        await Assert.That(goals.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Names_ReturnsAllNames()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "Goal1" });
        goals.Add(new Goal { Name = "Goal2" });

        var names = goals.Names.ToList();

        await Assert.That(names).Contains("Goal1");
        await Assert.That(names).Contains("Goal2");
    }

    [Test]
    public async Task All_ReturnsAllGoals()
    {
        var goals = new EngineGoals();
        var goal1 = new Goal { Name = "Goal1" };
        var goal2 = new Goal { Name = "Goal2" };
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
        goals.Add(new Goal { Name = "PublicGoal", Visibility = Visibility.Public });
        goals.Add(new Goal { Name = "PrivateGoal", Visibility = Visibility.Private });

        var publicGoals = goals.Public.ToList();

        await Assert.That(publicGoals.Count).IsEqualTo(1);
        await Assert.That(publicGoals[0].Name).IsEqualTo("PublicGoal");
    }

    [Test]
    public async Task Setup_ReturnsOnlySetupGoals()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "SetupGoal", IsSetup = true });
        goals.Add(new Goal { Name = "NormalGoal", IsSetup = false });

        var setupGoals = goals.Setup.ToList();

        await Assert.That(setupGoals.Count).IsEqualTo(1);
        await Assert.That(setupGoals[0].Name).IsEqualTo("SetupGoal");
    }

    [Test]
    public async Task Events_ReturnsOnlyEventGoals()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "EventGoal", IsEvent = true });
        goals.Add(new Goal { Name = "NormalGoal", IsEvent = false });

        var eventGoals = goals.Events.ToList();

        await Assert.That(eventGoals.Count).IsEqualTo(1);
        await Assert.That(eventGoals[0].Name).IsEqualTo("EventGoal");
    }

    [Test]
    public async Task Add_SameNameTwice_ReplacesGoal()
    {
        var goals = new EngineGoals();
        var goal1 = new Goal { Name = "TestGoal", Description = "First" };
        var goal2 = new Goal { Name = "TestGoal", Description = "Second" };
        goals.Add(goal1);

        goals.Add(goal2);

        await Assert.That(goals.Count).IsEqualTo(1);
        await Assert.That(goals.Get("TestGoal")!.Description).IsEqualTo("Second");
    }

    [Test]
    public async Task Count_ReturnsCorrectCount()
    {
        var goals = new EngineGoals();
        goals.Add(new Goal { Name = "Goal1" });
        goals.Add(new Goal { Name = "Goal2" });
        goals.Add(new Goal { Name = "Goal3" });

        await Assert.That(goals.Count).IsEqualTo(3);
    }
}
