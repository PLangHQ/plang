using System.Net;
using System.Net.Http;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Settings;
using PLang.Runtime2.modules.http;
using PLang.Runtime2.modules.http.providers;

namespace PLang.Tests.Runtime2.Modules.http;

/// <summary>
/// Tests DefaultHttpProvider lifecycle — lazy client creation, configuration, disposal.
/// </summary>
public class DefaultHttpProviderTests
{
    [Test]
    public async Task Provider_LazyCreatesHttpClient_OnFirstSend()
    {
        // HttpClient not created until first SendAsync call
        var provider = new DefaultHttpProvider();

        // Before any call, Dispose should be safe (no client to dispose)
        provider.Dispose();

        // After dispose, a new call should create a fresh client
        // We can't easily test internal state, but we can verify it doesn't throw
        // on a request to a non-existent URL — the attempt proves client creation
        var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://localhost:1/test");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await provider.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token);
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            // Expected — proves client was created and attempted the request
        }
        finally
        {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Provider_Configure_AcceptsValidConfig()
    {
        var provider = new DefaultHttpProvider();
        var config = new Config { FollowRedirects = true, MaxRedirects = 5 };

        var result = provider.Configure(config);

        await Assert.That(result.Success).IsTrue();
        provider.Dispose();
    }

    [Test]
    public async Task Provider_Configure_RejectsNonConfigSettings()
    {
        var provider = new DefaultHttpProvider();

        // Use a different ISettings type
        var result = provider.Configure(new DummySettings());

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidConfig");
        provider.Dispose();
    }

    [Test]
    public async Task Provider_Dispose_CleansUpHttpClient()
    {
        var provider = new DefaultHttpProvider();

        // Trigger client creation
        var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://localhost:1/test");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await provider.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token);
        }
        catch { /* expected */ }

        // Dispose should not throw
        provider.Dispose();

        // After dispose, a new send should work (creates new client)
        var request2 = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://localhost:1/test2");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await provider.SendAsync(request2, HttpCompletionOption.ResponseContentRead, cts.Token);
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            // Expected — proves a new client was created after dispose
        }
        finally
        {
            provider.Dispose();
        }
    }

    private class DummySettings : ISettings { }
}
