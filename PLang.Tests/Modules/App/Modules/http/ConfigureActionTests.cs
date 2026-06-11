using System.Net;
using System.Text;
using app.actor.context;
using app.variable;
using app.module.code;
using app.module.http;
using app.module.http.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.http;

/// <summary>
/// Tests configure action with real Default.
/// Configure doesn't need HTTP transport — it writes to the settings scope chain.
/// </summary>
public class ConfigureActionTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_cfg_" + Guid.NewGuid().ToString("N")[..8]);
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

    private global::app.actor.context.@this Ctx => _app.System.Context;

    [Test]
    public async Task Configure_SetsTimeoutOnScopeChain()
    {
        var action = new configure { Context = Ctx, TimeoutInSec = (global::app.type.number.@this)60 };
        var result = await action.Run();

        await result.IsSuccess();
        var view = _app.Config.For<Config>(Ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(60);
    }

    [Test]
    public async Task Configure_BaseUrl_WrittenToScope()
    {
        var action = new configure { Context = Ctx, BaseUrl = (global::app.type.text.@this)"https://api.example.com/v2" };
        var result = await action.Run();

        await result.IsSuccess();
        var view = _app.Config.For<Config>(Ctx);
        await Assert.That(view.Resolve<string?>("BaseUrl", null)).IsEqualTo("https://api.example.com/v2");
    }

    [Test]
    public async Task Configure_DefaultTrue_SetsEngineLevel()
    {
        var action = new configure { Context = Ctx, TimeoutInSec = (global::app.type.number.@this)120, Default = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsSuccess();

        // New context should still see the engine-level default
        var newContext = _app.User.Context;
        var view = _app.Config.For<Config>(newContext);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(120);
    }

    [Test]
    public async Task Configure_DefaultFalse_ScopedToContext()
    {
        var action = new configure { Context = Ctx, TimeoutInSec = (global::app.type.number.@this)90, Default = (global::app.type.@bool.@this)false };
        var result = await action.Run();

        await result.IsSuccess();

        // Current context sees 90
        var view = _app.Config.For<Config>(Ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(90);

        // New context sees class default (30)
        var newContext = _app.User.Context;
        var newView = _app.Config.For<Config>(newContext);
        await Assert.That(newView.Resolve("TimeoutInSec", 30)).IsEqualTo(30);
    }

    [Test]
    public async Task Configure_RedirectLock_AfterFirstRequest()
    {
        // Create a handler and make a request to lock the client
        var handler = new MockHttpMessageHandler();
        var provider = new Default(handler);
        _app.Code.Register<IHttp>(provider);
        _app.Code.SetDefault<IHttp>("default");

        var req = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://example.com", Unsigned = (global::app.type.@bool.@this)true };
        await req.Run(); // locks the client

        var action = new configure { Context = Ctx, FollowRedirects = (global::app.type.@bool.@this)false };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ConfigLocked");
    }

    [Test]
    public async Task Configure_NullProperties_NotWritten()
    {
        // Only set BaseUrl, leave everything else null
        var action = new configure { Context = Ctx, BaseUrl = (global::app.type.text.@this)"https://api.example.com" };
        var result = await action.Run();

        await result.IsSuccess();
        var view = _app.Config.For<Config>(Ctx);
        // TimeoutInSec was not set — should still be class default
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(30);
    }

    [Test]
    public async Task Configure_PerStepTimeout_OverridesConfiguredTimeout()
    {
        // Configure module-level timeout = 60
        var configAction = new configure { Context = Ctx, TimeoutInSec = (global::app.type.number.@this)60 };
        await configAction.Run();

        // Verify config has 60
        var view = _app.Config.For<Config>(Ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(60);

        // Make a request with per-step TimeoutInSec = (global::app.type.number.@this)1
        // If timeout is respected, a slow handler will trigger Timeout error
        var handler = new MockHttpMessageHandler();
        handler.SlowDelay = 3000; // 3 seconds
        var provider = new Default(handler) { Name = "timeout-test" };
        _app.Code.Register<IHttp>(provider);
        _app.Code.SetDefault<IHttp>("timeout-test");

        var requestAction = new request
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/slow",
            TimeoutInSec = (global::app.type.number.@this)1, // per-step override: 1 second
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await requestAction.Run();

        // Should timeout at 1s (per-step), not wait 60s (configured)
        await result.IsFailure();
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
