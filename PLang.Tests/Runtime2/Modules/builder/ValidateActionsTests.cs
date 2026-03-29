using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.builder;

/// <summary>
/// Tests for builder.validateActions — validates LLM-returned actions exist in engine.Modules,
/// resolves GoalCall paths, fills defaults from [Default] attributes.
/// </summary>
public class ValidateActionsTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_validate_" + Guid.NewGuid().ToString("N")[..8]);
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
    public async Task ValidateActions_ValidActions_ReturnsOk()
    {
        // All actions found in engine.Modules → Data.Ok(true)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ValidateActions_UnknownAction_ReturnsError()
    {
        // Action not in module registry → error Data with action name in message
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ValidateActions_GoalCallPath_Resolved()
    {
        // GoalCall PrPath resolved by scanning .build/ for .pr files,
        // falling back to .goal files in the source tree
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ValidateActions_DynamicNames_Skipped()
    {
        // GoalCall names containing % (variable references) are not resolved
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ValidateActions_DefaultsFilled()
    {
        // Missing parameters with [Default] attribute get default values applied
        Assert.Fail("Not implemented");
    }
}
