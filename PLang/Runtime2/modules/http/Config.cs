using PLang.Runtime2.Engine.Config;

namespace PLang.Runtime2.modules.http;

/// <summary>
/// HTTP module configuration. Implements IConfig for scope-chain resolution.
/// PLang: "configure http, base url https://api.example.com, timeout 60"
/// Resolution order: per-step parameter → scope chain → class default.
/// </summary>
public partial class Config : IConfig
{
    public int TimeoutInSec { get; set; } = 30;
    public string? BaseUrl { get; set; }
    public Dictionary<string, object>? DefaultHeaders { get; set; }
    public string ContentType { get; set; } = "application/json";
    public string Encoding { get; set; } = "utf-8";
    public bool Unsigned { get; set; } = false;
    public bool FollowRedirects { get; set; } = true;
    public int MaxRedirects { get; set; } = 10;

    /// <summary>Max response body size in bytes. Default 100MB. Protects against OOM from untrusted servers.</summary>
    public long MaxResponseSize { get; set; } = 100 * 1024 * 1024;

    /// <summary>Max SSE message buffer size in bytes. Default 10MB. Protects against unbounded data: lines without blank boundaries.</summary>
    public long MaxSSEBufferSize { get; set; } = 10 * 1024 * 1024;
}
