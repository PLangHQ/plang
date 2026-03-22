using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.modules.http.providers;

namespace PLang.Runtime2.modules.http;

/// <summary>
/// Sets HTTP module configuration on the in-memory scope chain.
/// Per-step parameters on request/download/upload override config values.
/// Config values override class defaults.
/// </summary>
[Action("configure", Cacheable = false)]
public partial class configure : IContext
{
    public partial int? TimeoutInSec { get; init; }
    public partial string? BaseUrl { get; init; }
    public partial Dictionary<string, object>? DefaultHeaders { get; init; }
    public partial string? ContentType { get; init; }
    public partial string? Encoding { get; init; }
    public partial bool? Unsigned { get; init; }
    public partial bool? FollowRedirects { get; init; }
    public partial int? MaxRedirects { get; init; }

    [Default(false)]
    public partial bool Default { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine;
        var isDefault = Default;

        // Write non-null values to scope chain
        if (TimeoutInSec.HasValue)
            engine.Settings.Set("http.TimeoutInSec", TimeoutInSec.Value, Context, isDefault);
        if (BaseUrl != null)
            engine.Settings.Set("http.BaseUrl", BaseUrl, Context, isDefault);
        if (DefaultHeaders != null)
            engine.Settings.Set("http.DefaultHeaders", DefaultHeaders, Context, isDefault);
        if (ContentType != null)
            engine.Settings.Set("http.ContentType", ContentType, Context, isDefault);
        if (Encoding != null)
            engine.Settings.Set("http.Encoding", Encoding, Context, isDefault);
        if (Unsigned.HasValue)
            engine.Settings.Set("http.Unsigned", Unsigned.Value, Context, isDefault);
        if (FollowRedirects.HasValue)
            engine.Settings.Set("http.FollowRedirects", FollowRedirects.Value, Context, isDefault);
        if (MaxRedirects.HasValue)
            engine.Settings.Set("http.MaxRedirects", MaxRedirects.Value, Context, isDefault);

        // Apply handler-level settings to provider if FollowRedirects or MaxRedirects changed
        if (FollowRedirects.HasValue || MaxRedirects.HasValue)
        {
            var providerResult = engine.Providers.Get<IHttpProvider>();
            if (!providerResult.Success) return providerResult;

            var config = new Config
            {
                FollowRedirects = FollowRedirects ?? engine.Settings.For<Config>(Context).Resolve("FollowRedirects", true),
                MaxRedirects = MaxRedirects ?? engine.Settings.For<Config>(Context).Resolve("MaxRedirects", 10)
            };

            var configResult = providerResult.Value!.Configure(config);
            if (!configResult.Success) return configResult;
        }

        return Data.Ok();
    }
}
