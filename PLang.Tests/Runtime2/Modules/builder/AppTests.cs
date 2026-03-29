using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.builder;

/// <summary>
/// Tests for builder.getApp and builder.saveApp — load/create and save app.pr metadata.
/// </summary>
public class AppTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_app_" + Guid.NewGuid().ToString("N")[..8]);
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
    public async Task GetApp_LoadsExistingAppPr()
    {
        // When .build/app.pr exists, reads and deserializes as AppData
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetApp_CreatesNewWhenMissing()
    {
        // When .build/app.pr doesn't exist, creates new AppData with GUID and Version = "0.2"
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SaveApp_UpdatesTimestamp()
    {
        // App.Updated timestamp is set before writing to disk
        Assert.Fail("Not implemented");
    }
}
