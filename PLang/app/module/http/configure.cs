using app.variable;
using app.module.http.code;

namespace app.module.http;

/// <summary>
/// Configures HTTP module defaults via the scope chain.
/// Non-null properties are written to the current scope; null properties are left unchanged.
/// Use Default=true to write to the app-wide default scope.
/// </summary>
[Action("configure", Cacheable = false)]
public partial class configure : IContext, IConfigure<Config>
{
    /// <summary>Default request timeout in seconds.</summary>
    public partial data.@this<int>? TimeoutInSec { get; init; }

    /// <summary>Base URL for resolving relative request URLs.</summary>
    public partial data.@this<string>? BaseUrl { get; init; }

    /// <summary>Default headers merged into every request.</summary>
    public partial data.@this<Dictionary<string, object>>? DefaultHeaders { get; init; }

    /// <summary>Default Content-Type for request bodies.</summary>
    public partial data.@this<string>? ContentType { get; init; }

    /// <summary>Default character encoding for request bodies.</summary>
    public partial data.@this<string>? Encoding { get; init; }

    /// <summary>When true, disables request signing by default.</summary>
    public partial data.@this<bool>? Unsigned { get; init; }

    /// <summary>Whether to follow HTTP redirects. Default: true.</summary>
    public partial data.@this<bool>? FollowRedirects { get; init; }

    /// <summary>Maximum number of redirects to follow. Default: 10.</summary>
    public partial data.@this<int>? MaxRedirects { get; init; }

    /// <summary>When true, writes config to app-wide default scope instead of current scope.</summary>
    [Default(false)]
    public partial data.@this<bool> Default { get; init; }

    [Code]
    public partial IHttp Http { get; }

    public async Task<data.@this> Run() => Http.Configure(this);
}
