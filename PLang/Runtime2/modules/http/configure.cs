using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.http.providers;

namespace PLang.Runtime2.modules.http;

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
        var provider = Context.Engine.Providers.Get<IHttpProvider>();
        if (!provider.Success) return provider;
        return provider.Value!.Configure(this);
    }
}
