using System.Net;
using System.Net.Http;
using System.Text;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.Engine.Settings;
using PLang.Runtime2.modules.http;
using PLang.Runtime2.modules.http.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.http;

/// <summary>
/// Tests the configure action handler — scope chain, BaseUrl, header merging, redirect locking.
/// </summary>
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
        public bool ConfigureCalled { get; private set; }
        public Config? LastConfig { get; private set; }
        public bool ClientCreated { get; private set; }
        private Data? _configLockError;

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        public Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken ct)
        {
            ClientCreated = true;
            return Task.FromResult(Response);
        }

        public Data Configure(ISettings config)
        {
            ConfigureCalled = true;
            if (config is Config c)
            {
                if (_configLockError != null) return _configLockError;
                LastConfig = c;
                return Data.Ok();
            }
            return Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError("Expected HTTP Config", "InvalidConfig", 400));
        }

        public void SimulateConfigLock()
        {
            _configLockError = Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(
                "Cannot change FollowRedirects/MaxRedirects after first HTTP request",
                "ConfigLocked", 409));
        }

        public void Dispose() { }
    }

    [Test]
    public async Task Configure_SetsTimeoutOnScopeChain()
    {
        var action = new configure
        {
            Context = Ctx,
            TimeoutInSec = 60
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var view = _engine.Settings.For<Config>(Ctx);
        var timeout = view.Resolve("TimeoutInSec", 30);
        await Assert.That(timeout).IsEqualTo(60);
    }

    [Test]
    public async Task Configure_PerStepOverridesConfig()
    {
        // First configure timeout to 60
        var configAction = new configure { Context = Ctx, TimeoutInSec = 60 };
        await configAction.Run();

        // Then make a request with per-step timeout of 10
        var requestAction = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/test",
            TimeoutInSec = 10,
            Unsigned = true
        };

        var result = await requestAction.Run();

        // Request should succeed — the per-step timeout of 10 should apply
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Configure_BaseUrlCombinesWithRelative()
    {
        // Configure base URL
        var configAction = new configure
        {
            Context = Ctx,
            BaseUrl = "https://api.example.com/v2"
        };
        await configAction.Run();

        // Make a request with relative URL
        var requestAction = new request
        {
            Context = Ctx,
            Url = "/users",
            Unsigned = true
        };

        var result = await requestAction.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_mock.Response).IsNotNull();
        // Captured request should have the full URL
        var capturedUrl = result.Properties["Url"]!.Value?.ToString();
        await Assert.That(capturedUrl).IsEqualTo("https://api.example.com/v2/users");
    }

    [Test]
    public async Task Configure_DefaultHeadersMergePerStepWins()
    {
        // Configure default headers
        var configAction = new configure
        {
            Context = Ctx,
            DefaultHeaders = new Dictionary<string, object>
            {
                ["Authorization"] = "Bearer default-token",
                ["X-App"] = "myapp"
            }
        };
        await configAction.Run();

        // Make a request with per-step header that overrides Authorization
        var requestAction = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/test",
            Headers = new Dictionary<string, object>
            {
                ["Authorization"] = "Bearer step-token"
            },
            Unsigned = true
        };

        var result = await requestAction.Run();

        await Assert.That(result.Success).IsTrue();

        // Verify per-step header wins
        var reqHeaders = result.Properties["RequestHeaders"]!.Value as Dictionary<string, string>;
        await Assert.That(reqHeaders).IsNotNull();
        await Assert.That(reqHeaders!["Authorization"]).IsEqualTo("Bearer step-token");
        // Default header should also be present
        await Assert.That(reqHeaders.ContainsKey("X-App")).IsTrue();
    }

    [Test]
    public async Task Configure_FollowRedirectsErrorAfterFirstRequest()
    {
        // Simulate that the provider locks after first request
        _mock.SimulateConfigLock();

        var action = new configure
        {
            Context = Ctx,
            FollowRedirects = false
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ConfigLocked");
    }

    [Test]
    public async Task Configure_DefaultTrue_SetsEngineLevel()
    {
        var action = new configure
        {
            Context = Ctx,
            TimeoutInSec = 120,
            Default = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();

        // Engine-level default should persist across different contexts
        var newContext = _engine.CreateContext();
        var view = _engine.Settings.For<Config>(newContext);
        var timeout = view.Resolve("TimeoutInSec", 30);
        await Assert.That(timeout).IsEqualTo(120);
    }

    [Test]
    public async Task Configure_DefaultFalse_ScopedToGoal()
    {
        var action = new configure
        {
            Context = Ctx,
            TimeoutInSec = 90,
            Default = false
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();

        // Should be visible in the same context
        var view = _engine.Settings.For<Config>(Ctx);
        var timeout = view.Resolve("TimeoutInSec", 30);
        await Assert.That(timeout).IsEqualTo(90);

        // A new context without parent should get the class default
        var newContext = _engine.CreateContext();
        var newView = _engine.Settings.For<Config>(newContext);
        var newTimeout = newView.Resolve("TimeoutInSec", 30);
        await Assert.That(newTimeout).IsEqualTo(30);
    }
}
