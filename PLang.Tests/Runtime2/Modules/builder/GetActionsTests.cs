using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.builder;

/// <summary>
/// Tests for builder.getActions — reflects all registered actions with parameter schemas
/// for the LLM prompt. Parameter metadata comes from reflecting action type properties.
/// </summary>
public class GetActionsTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_actions_" + Guid.NewGuid().ToString("N")[..8]);
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
    public async Task GetActions_ReturnsAllModulesAndActions()
    {
        // Should iterate engine.Modules and return entries for every registered action
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetActions_ParameterTypes_IncludeNullableMarkers()
    {
        // Nullable properties (e.g., string?) should have "?" suffix in type name
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetActions_VariableNameParams_Marked()
    {
        // Properties with [VariableName] attribute should be flagged in the output
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetActions_DefaultValues_Included()
    {
        // Properties with [Default] attribute should have default values surfaced
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetActions_CacheableFlag_FromActionAttribute()
    {
        // Actions with [Action(Cacheable = false)] should reflect Cacheable = false
        Assert.Fail("Not implemented");
    }
}
