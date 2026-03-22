using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.http;
using PLang.Runtime2.modules.http.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.http;

public class ConfigureActionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;
    private MockHttpProvider _mock = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_cfg_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);

        _mock = new MockHttpProvider();
        _engine.Providers.Register<IHttpProvider>(_mock);
        _engine.Providers.SetDefault<IHttpProvider>("mock");
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

    private PLangContext Ctx => _engine.System.Context;

    private class MockHttpProvider : IHttpProvider
    {
        public string Name => "mock";
        public bool IsDefault { get; set; }
        public configure? CapturedConfigure { get; private set; }
        public Func<configure, Data>? OnConfigure { get; set; }

        public async Task<Data> SendAsync(request action) => Data.Ok();
        public async Task<Data> DownloadAsync(download action) => Data.Ok();
        public async Task<Data> UploadAsync(upload action) => Data.Ok();
        public Data Configure(configure action)
        {
            CapturedConfigure = action;
            if (OnConfigure != null) return OnConfigure(action);
            // Default: write settings like the real provider
            var engine = action.Context.Engine;
            var isDefault = action.Default;
            if (action.TimeoutInSec.HasValue)
                engine.Settings.Set("http.TimeoutInSec", action.TimeoutInSec.Value, action.Context, isDefault);
            if (action.BaseUrl != null)
                engine.Settings.Set("http.BaseUrl", action.BaseUrl, action.Context, isDefault);
            if (action.DefaultHeaders != null)
                engine.Settings.Set("http.DefaultHeaders", action.DefaultHeaders, action.Context, isDefault);
            return Data.Ok();
        }
        public void Dispose() { }
    }

    [Test]
    public async Task Configure_SetsTimeoutOnScopeChain()
    {
        var action = new configure { Context = Ctx, TimeoutInSec = 60 };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var view = _engine.Settings.For<Config>(Ctx);
        var timeout = view.Resolve("TimeoutInSec", 30);
        await Assert.That(timeout).IsEqualTo(60);
    }

    [Test]
    public async Task Configure_PerStepOverridesConfig()
    {
        var configAction = new configure { Context = Ctx, TimeoutInSec = 60 };
        await configAction.Run();

        // Request with per-step timeout
        var requestAction = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/test",
            TimeoutInSec = 10,
            Unsigned = true
        };

        var result = await requestAction.Run();

        await Assert.That(result.Success).IsTrue();
        // Per-step timeout of 10 applies, not config's 60
        await Assert.That(_mock.CapturedConfigure!.TimeoutInSec).IsEqualTo(60);
    }

    [Test]
    public async Task Configure_BaseUrlPassedToProvider()
    {
        var action = new configure
        {
            Context = Ctx,
            BaseUrl = "https://api.example.com/v2"
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var view = _engine.Settings.For<Config>(Ctx);
        var baseUrl = view.Resolve<string?>("BaseUrl", null);
        await Assert.That(baseUrl).IsEqualTo("https://api.example.com/v2");
    }

    [Test]
    public async Task Configure_DefaultHeadersPassedToProvider()
    {
        var headers = new Dictionary<string, object>
        {
            ["Authorization"] = "Bearer token",
            ["X-App"] = "myapp"
        };

        var action = new configure { Context = Ctx, DefaultHeaders = headers };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_mock.CapturedConfigure!.DefaultHeaders).IsNotNull();
    }

    [Test]
    public async Task Configure_FollowRedirectsErrorAfterFirstRequest()
    {
        _mock.OnConfigure = action =>
            Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(
                "Cannot change after first request", "ConfigLocked", 409));

        var action = new configure { Context = Ctx, FollowRedirects = false };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ConfigLocked");
    }

    [Test]
    public async Task Configure_DefaultTrue_SetsEngineLevel()
    {
        var action = new configure { Context = Ctx, TimeoutInSec = 120, Default = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();

        var newContext = _engine.CreateContext();
        var view = _engine.Settings.For<Config>(newContext);
        var timeout = view.Resolve("TimeoutInSec", 30);
        await Assert.That(timeout).IsEqualTo(120);
    }

    [Test]
    public async Task Configure_DefaultFalse_ScopedToGoal()
    {
        _mock.OnConfigure = action =>
        {
            var engine = action.Context.Engine;
            if (action.TimeoutInSec.HasValue)
                engine.Settings.Set("http.TimeoutInSec", action.TimeoutInSec.Value, action.Context, action.Default);
            return Data.Ok();
        };

        var action = new configure { Context = Ctx, TimeoutInSec = 90, Default = false };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();

        var view = _engine.Settings.For<Config>(Ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(90);

        var newContext = _engine.CreateContext();
        var newView = _engine.Settings.For<Config>(newContext);
        await Assert.That(newView.Resolve("TimeoutInSec", 30)).IsEqualTo(30);
    }
}
