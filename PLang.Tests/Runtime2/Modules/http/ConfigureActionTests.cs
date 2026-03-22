using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.http;

/// <summary>
/// Tests the configure action handler — scope chain, BaseUrl, header merging, redirect locking.
/// </summary>
public class ConfigureActionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_cfg_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private PLangContext Ctx => _engine.System.Context;

    [Test]
    public async Task Configure_SetsTimeoutOnScopeChain()
    {
        // Config values stored via engine.Settings, retrievable via Settings.For<Config>(context)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Configure_PerStepOverridesConfig()
    {
        // Per-step TimeoutInSec on request overrides configured value
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Configure_BaseUrlCombinesWithRelative()
    {
        // BaseUrl "https://api.example.com/v2" + request URL "/users" → "https://api.example.com/v2/users"
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Configure_DefaultHeadersMergePerStepWins()
    {
        // DefaultHeaders merged with per-step headers, per-step wins on conflict
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Configure_FollowRedirectsErrorAfterFirstRequest()
    {
        // Changing FollowRedirects after first HTTP request → Data.FromError(ServiceError("ConfigLocked"))
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Configure_DefaultTrue_SetsEngineLevel()
    {
        // Default=true → config persists across goals within execution (engine-level scope)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Configure_DefaultFalse_ScopedToGoal()
    {
        // Default=false (default) → config scoped to current goal, not visible in other goals
        Assert.Fail("Not implemented");
    }
}
