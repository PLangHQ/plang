using App.Actor.Context;
using App.Variables;
using App.modules.http;
using App.modules.http.providers;
using PLangEngine = App.@this;

namespace PLang.Tests.App.Modules.http;

/// <summary>
/// Tests DefaultHttpProvider directly — configure behavior, lifecycle.
/// </summary>
public class DefaultHttpProviderTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_prov_" + Guid.NewGuid().ToString("N")[..8]);
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
        catch { /* best effort cleanup */ }
    }

    private global::App.Actor.Context.@this Ctx => _engine.System.Context;

    [Test]
    public async Task Provider_Configure_AcceptsValidConfig()
    {
        var provider = new DefaultHttpProvider();
        var action = new configure
        {
            Context = Ctx,
            FollowRedirects = true,
            MaxRedirects = 5
        };

        var result = provider.Configure(action);

        await Assert.That(result.Success).IsTrue();
        provider.Dispose();
    }

    [Test]
    public async Task Provider_Configure_SetsTimeout()
    {
        var provider = new DefaultHttpProvider();
        var action = new configure
        {
            Context = Ctx,
            TimeoutInSec = 60
        };

        var result = provider.Configure(action);

        await Assert.That(result.Success).IsTrue();
        // Verify via settings scope
        var view = _engine.Config.For<Config>(Ctx);
        var timeout = view.Resolve("TimeoutInSec", 30);
        await Assert.That(timeout).IsEqualTo(60);
        provider.Dispose();
    }

    [Test]
    public async Task Provider_Configure_SetsBaseUrl()
    {
        var provider = new DefaultHttpProvider();
        var action = new configure
        {
            Context = Ctx,
            BaseUrl = "https://api.example.com"
        };

        var result = provider.Configure(action);

        await Assert.That(result.Success).IsTrue();
        var view = _engine.Config.For<Config>(Ctx);
        var baseUrl = view.Resolve<string?>("BaseUrl", null);
        await Assert.That(baseUrl).IsEqualTo("https://api.example.com");
        provider.Dispose();
    }

    [Test]
    public async Task Provider_Dispose_DoesNotThrow()
    {
        var provider = new DefaultHttpProvider();
        provider.Dispose();
        // Double dispose should also be safe
        provider.Dispose();
    }
}
