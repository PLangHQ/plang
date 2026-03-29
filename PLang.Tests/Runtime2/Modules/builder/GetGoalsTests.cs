using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.builder;

/// <summary>
/// Tests for builder.getGoals — finds .goal files under a path, parses via GoalFile,
/// filters out system goals, and merges existing .pr data by matching goal names.
/// </summary>
public class GetGoalsTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_getgoals_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _engine.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort */ }
    }

    [Test]
    public async Task GetGoals_ParsesGoalFilesFromFolder()
    {
        // Write a .goal file to temp dir, call getGoals, verify goals returned
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetGoals_ExcludesSystemGoals()
    {
        // .goal files under /system/ path should be filtered out
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetGoals_MergesExistingPrData()
    {
        // When a .pr file exists at PrPath, deserialize as List<Goal>,
        // match by Name, call goal.MergeFrom(existingGoal) — LLM work preserved
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetGoals_EmptyFolder_ReturnsEmptyList()
    {
        // No .goal files in folder → empty list, no error
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetGoals_CorruptPrFile_IgnoresAndReparses()
    {
        // When the existing .pr file contains invalid JSON, getGoals should not crash —
        // treat it as if no .pr exists and let the LLM rebuild from scratch
        Assert.Fail("Not implemented");
    }
}
