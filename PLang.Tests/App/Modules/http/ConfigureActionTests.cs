using System.Net;
using System.Text;
using App.Actor.Context;
using App.Variables;
using App.Providers;
using App.modules.http;
using App.modules.http.providers;
using PLangEngine = App.@this;

namespace PLang.Tests.App.Modules.http;

/// <summary>
/// Tests configure action with real DefaultHttpProvider.
/// Configure doesn't need HTTP transport — it writes to the settings scope chain.
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
    public async Task Configure_SetsTimeoutOnScopeChain()
    {
        var action = new configure { Context = Ctx, TimeoutInSec = 60 };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var view = _engine.Config.For<Config>(Ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(60);
    }

    [Test]
    public async Task Configure_BaseUrl_WrittenToScope()
    {
        var action = new configure { Context = Ctx, BaseUrl = "https://api.example.com/v2" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var view = _engine.Config.For<Config>(Ctx);
        await Assert.That(view.Resolve<string?>("BaseUrl", null)).IsEqualTo("https://api.example.com/v2");
    }

    [Test]
    public async Task Configure_DefaultTrue_SetsEngineLevel()
    {
        var action = new configure { Context = Ctx, TimeoutInSec = 120, Default = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();

        // New context should still see the engine-level default
        var newContext = _engine.CreateContext();
        var view = _engine.Config.For<Config>(newContext);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(120);
    }

    [Test]
    public async Task Configure_DefaultFalse_ScopedToContext()
    {
        var action = new configure { Context = Ctx, TimeoutInSec = 90, Default = false };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();

        // Current context sees 90
        var view = _engine.Config.For<Config>(Ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(90);

        // New context sees class default (30)
        var newContext = _engine.CreateContext();
        var newView = _engine.Config.For<Config>(newContext);
        await Assert.That(newView.Resolve("TimeoutInSec", 30)).IsEqualTo(30);
    }

    [Test]
    public async Task Configure_RedirectLock_AfterFirstRequest()
    {
        // Create a handler and make a request to lock the client
        var handler = new MockHttpMessageHandler();
        var provider = new DefaultHttpProvider(handler);
        _engine.Providers.Register<IHttpProvider>(provider);
        _engine.Providers.SetDefault<IHttpProvider>("default");

        var req = new request { Context = Ctx, Url = "https://example.com", Unsigned = true };
        await req.Run(); // locks the client

        var action = new configure { Context = Ctx, FollowRedirects = false };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ConfigLocked");
    }

    [Test]
    public async Task Configure_NullProperties_NotWritten()
    {
        // Only set BaseUrl, leave everything else null
        var action = new configure { Context = Ctx, BaseUrl = "https://api.example.com" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var view = _engine.Config.For<Config>(Ctx);
        // TimeoutInSec was not set — should still be class default
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(30);
    }

    [Test]
    public async Task Configure_PerStepTimeout_OverridesConfiguredTimeout()
    {
        // Configure module-level timeout = 60
        var configAction = new configure { Context = Ctx, TimeoutInSec = 60 };
        await configAction.Run();

        // Verify config has 60
        var view = _engine.Config.For<Config>(Ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(60);

        // Make a request with per-step TimeoutInSec = 1
        // If timeout is respected, a slow handler will trigger Timeout error
        var handler = new MockHttpMessageHandler();
        handler.SlowDelay = 3000; // 3 seconds
        var provider = new DefaultHttpProvider(handler) { Name = "timeout-test" };
        _engine.Providers.Register<IHttpProvider>(provider);
        _engine.Providers.SetDefault<IHttpProvider>("timeout-test");

        var requestAction = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/slow",
            TimeoutInSec = 1, // per-step override: 1 second
            Unsigned = true
        };
        var result = await requestAction.Run();

        // Should timeout at 1s (per-step), not wait 60s (configured)
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
    }

    private class MockHttpMessageHandler : System.Net.Http.HttpMessageHandler
    {
        public int SlowDelay { get; set; }

        protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (SlowDelay > 0)
                await Task.Delay(SlowDelay, cancellationToken);

            return new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent("{}", Encoding.UTF8, "application/json")
            };
        }
    }
}
