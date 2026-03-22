using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.http.providers;
using PLang.Runtime2.modules.signing;

namespace PLang.Runtime2.modules.http;

[Action("upload", Cacheable = false)]
public partial class upload : IContext
{
    public partial string Url { get; init; }
    public partial object Content { get; init; }

    [Default(HttpMethod.POST)]
    public partial HttpMethod Method { get; init; }

    public partial Dictionary<string, object>? Headers { get; init; }

    [Default("utf-8")]
    public partial string Encoding { get; init; }

    [Default(30)]
    public partial int TimeoutInSec { get; init; }

    [Default(false)]
    public partial bool Unsigned { get; init; }

    public partial sign? SignOptions { get; init; }
    public partial ContentAs? As { get; init; }
    public partial GoalCall? OnProgress { get; init; }

    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IHttpProvider>();
        if (!provider.Success) return provider;
        return await provider.Value!.UploadAsync(this);
    }
}
