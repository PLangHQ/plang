using PLang.Runtime2;

namespace PLang.Tests.Runtime2.Foundation;

/// <summary>
/// Proves Fix #10: Goal.PrPath has a no-op setter (set { }).
/// Setting PrPath silently does nothing — it's always derived from Path.
/// After the fix, PrPath will be init-only so the setter can only be used during construction.
/// These tests verify PrPath derivation is correct.
/// </summary>
public class GoalPrPathTests
{
    [Test]
    public async Task PrPath_DerivedFromPath_Correctly()
    {
        var goal = new Goal { Name = "Test", Path = "\\Test.goal" };
        await Assert.That(goal.PrPath).IsEqualTo("\\.build\\test.pr");
    }

    [Test]
    public async Task PrPath_SubDirectory_DerivedCorrectly()
    {
        var goal = new Goal { Name = "Inner", Path = "\\SubDir\\Inner.goal" };
        await Assert.That(goal.PrPath).IsEqualTo("\\SubDir\\.build\\inner.pr");
    }

    [Test]
    public async Task PrPath_ForwardSlashPath_DerivedCorrectly()
    {
        var goal = new Goal { Name = "Test", Path = "/Test.goal" };
        await Assert.That(goal.PrPath).IsEqualTo("/.build/test.pr");
    }

    [Test]
    public async Task PrPath_NullPath_ReturnsNull()
    {
        var goal = new Goal { Name = "Test" };
        await Assert.That(goal.PrPath).IsNull();
    }

    [Test]
    public async Task PrPath_UpdatesWhenPathChanges()
    {
        var goal = new Goal { Name = "Test", Path = "\\Test.goal" };
        await Assert.That(goal.PrPath).IsEqualTo("\\.build\\test.pr");

        goal.Path = "\\Other\\Test.goal";
        await Assert.That(goal.PrPath).IsEqualTo("\\Other\\.build\\test.pr");
    }
}
