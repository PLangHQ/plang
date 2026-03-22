using System.Net.Http;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Settings;

namespace PLang.Runtime2.modules.http.providers;

/// <summary>
/// Default HTTP provider. Lazily creates HttpClient on first request.
/// SocketsHttpHandler with configurable FollowRedirects/MaxRedirects.
/// Handler-level settings lock after first request.
/// HttpClient lives for the Engine's lifetime — disposed when Engine is disposed.
/// </summary>
public sealed class DefaultHttpProvider : IHttpProvider
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    private HttpClient? _client;
    private bool _followRedirects = true;
    private int _maxRedirects = 10;

    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        _client ??= CreateClient();
        return _client.SendAsync(request, completionOption, cancellationToken);
    }

    public Data Configure(ISettings settings)
    {
        if (settings is not Config config)
            return Data.FromError(new ServiceError("Expected HTTP Config", "InvalidConfig", 400));

        if (_client != null && (config.FollowRedirects != _followRedirects || config.MaxRedirects != _maxRedirects))
            return Data.FromError(new ServiceError(
                "Cannot change FollowRedirects/MaxRedirects after first HTTP request",
                "ConfigLocked", 409));

        _followRedirects = config.FollowRedirects;
        _maxRedirects = config.MaxRedirects;
        return Data.Ok();
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }

    private HttpClient CreateClient() => new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        AllowAutoRedirect = _followRedirects,
        MaxAutomaticRedirections = _maxRedirects
    });
}
