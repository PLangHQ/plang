using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.builder;

/// <summary>
/// Tests the engine.Building.IsEnabled guard — all builder actions should return
/// an error when building is not enabled.
/// </summary>
public class BuildingGuardTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_guard_" + Guid.NewGuid().ToString("N")[..8]);
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
    public async Task GetGoals_BuildingDisabled_ReturnsError()
    {
        // builder.getGoals with engine.Building.IsEnabled=false → Data.FromError
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetActions_BuildingDisabled_ReturnsError()
    {
        // builder.getActions with engine.Building.IsEnabled=false → Data.FromError
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ValidateActions_BuildingDisabled_ReturnsError()
    {
        // builder.validateActions with engine.Building.IsEnabled=false → Data.FromError
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SaveGoals_BuildingDisabled_ReturnsError()
    {
        // builder.saveGoals with engine.Building.IsEnabled=false → Data.FromError
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetApp_BuildingDisabled_ReturnsError()
    {
        // builder.getApp with engine.Building.IsEnabled=false → Data.FromError
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SaveApp_BuildingDisabled_ReturnsError()
    {
        // builder.saveApp with engine.Building.IsEnabled=false → Data.FromError
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task MergeStep_BuildingDisabled_ReturnsError()
    {
        // builder.mergeStep with engine.Building.IsEnabled=false → Data.FromError
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetTypeInfo_BuildingDisabled_ReturnsError()
    {
        // builder.getTypeInfo with engine.Building.IsEnabled=false → Data.FromError
        Assert.Fail("Not implemented");
    }
}
