using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.http.providers;

namespace PLang.Runtime2.modules.http;

/// <summary>
/// Configures HTTP module defaults via the scope chain.
/// Non-null properties are written to the current scope; null properties are left unchanged.
/// Use Default=true to write to the app-wide default scope.
/// </summary>
[Action("configure", Cacheable = false)]
public partial class configure : IContext, IConfigure<Config>
{
    /// <summary>Default request timeout in seconds.</summary>
    public partial int? TimeoutInSec { get; init; }

    /// <summary>Base URL for resolving relative request URLs.</summary>
    public partial string? BaseUrl { get; init; }

    /// <summary>Default headers merged into every request.</summary>
    public partial Dictionary<string, object>? DefaultHeaders { get; init; }

    /// <summary>Default Content-Type for request bodies.</summary>
    public partial string? ContentType { get; init; }

    /// <summary>Default character encoding for request bodies.</summary>
    public partial string? Encoding { get; init; }

    /// <summary>When true, disables request signing by default.</summary>
    public partial bool? Unsigned { get; init; }

    /// <summary>Whether to follow HTTP redirects. Default: true.</summary>
    public partial bool? FollowRedirects { get; init; }

    /// <summary>Maximum number of redirects to follow. Default: 10.</summary>
    public partial int? MaxRedirects { get; init; }

    /// <summary>When true, writes config to app-wide default scope instead of current scope.</summary>
    [Default(false)]
    public partial bool Default { get; init; }

    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IHttpProvider>();
        if (!provider.Success) return provider;
        return provider.Value!.Configure(this);
    }
}
