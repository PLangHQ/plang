using global::App.Actor.Context;
using global::App.Variables;
using global::App.modules.http;
using global::App.modules.http.code;
using PLangEngine = global::App.@this;

namespace PLang.Tests.App.Modules.http;

/// <summary>
/// Tests Default directly — configure behavior, lifecycle.
/// </summary>
public class DefaultHttpProviderTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_prov_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _app.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private global::App.Actor.Context.@this Ctx => _app.System.Context;

    [Test]
    public async Task Provider_Configure_AcceptsValidConfig()
    {
        var provider = new Default();
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
        var provider = new Default();
        var action = new configure
        {
            Context = Ctx,
            TimeoutInSec = 60
        };

        var result = provider.Configure(action);

        await Assert.That(result.Success).IsTrue();
        // Verify via settings scope
        var view = _app.Config.For<Config>(Ctx);
        var timeout = view.Resolve("TimeoutInSec", 30);
        await Assert.That(timeout).IsEqualTo(60);
        provider.Dispose();
    }

    [Test]
    public async Task Provider_Configure_SetsBaseUrl()
    {
        var provider = new Default();
        var action = new configure
        {
            Context = Ctx,
            BaseUrl = "https://api.example.com"
        };

        var result = provider.Configure(action);

        await Assert.That(result.Success).IsTrue();
        var view = _app.Config.For<Config>(Ctx);
        var baseUrl = view.Resolve<string?>("BaseUrl", null);
        await Assert.That(baseUrl).IsEqualTo("https://api.example.com");
        provider.Dispose();
    }

    [Test]
    public async Task Provider_Dispose_DoesNotThrow()
    {
        var provider = new Default();
        provider.Dispose();
        // Double dispose should also be safe
        provider.Dispose();
    }
}
