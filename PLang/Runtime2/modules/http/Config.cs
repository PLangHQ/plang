using PLang.Runtime2.Engine.Settings;

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
}
