using app.config;

namespace app.module.http;

/// <summary>
/// HTTP module configuration. Implements IConfig for scope-chain resolution.
/// PLang: "configure http, base url https://api.example.com, timeout 60"
/// Resolution order: per-step parameter → scope chain → class default.
/// </summary>
public partial class Config : IConfig
{
    /// <summary>Request timeout in seconds. Default: 30.</summary>
    public int TimeoutInSec { get; set; } = 30;

    /// <summary>Base URL for resolving relative request URLs. Null = URLs must be absolute.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Default headers merged into every request. Step-level headers win on conflict.</summary>
    public Dictionary<string, object>? DefaultHeaders { get; set; }

    /// <summary>Default Content-Type for request bodies. Default: "application/json".</summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>Default character encoding for request bodies. Default: "utf-8".</summary>
    public string Encoding { get; set; } = "utf-8";

    /// <summary>When true, skips signing for all requests by default. Default: false (signed).</summary>
    public bool Unsigned { get; set; } = false;

    /// <summary>Whether to follow HTTP redirects. Default: true.</summary>
    public bool FollowRedirects { get; set; } = true;

    /// <summary>Maximum number of redirects to follow. Default: 10.</summary>
    public int MaxRedirects { get; set; } = 10;

    /// <summary>Max response body size in bytes. Default 100MB. Protects against OOM from untrusted servers.</summary>
    public long MaxResponseSize { get; set; } = 100 * 1024 * 1024;

    /// <summary>Max SSE message buffer size in bytes. Default 10MB. Protects against unbounded data: lines without blank boundaries.</summary>
    public long MaxSSEBufferSize { get; set; } = 10 * 1024 * 1024;
}
