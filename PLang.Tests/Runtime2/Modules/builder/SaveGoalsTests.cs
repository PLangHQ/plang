using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder;
using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.builder;

/// <summary>
/// Tests for builder.saveGoals — serializes goals to a v0.2 .pr file.
/// One .goal file → one .pr file containing List&lt;Goal&gt;.
/// </summary>
public class SaveGoalsTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_savegoals_" + Guid.NewGuid().ToString("N")[..8]);
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
    public async Task SaveGoals_SerializesToPrPath()
    {
        // Writes List<Goal> as JSON to the derived PrPath from the first goal
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SaveGoals_CamelCase_NullsOmitted()
    {
        // JSON output uses camelCase property names and omits null values
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SaveGoals_MultipleGoals_SingleFile()
    {
        // Multiple goals from one .goal file → single .pr file containing all
        Assert.Fail("Not implemented");
    }
}
