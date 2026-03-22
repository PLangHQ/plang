using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.http.providers;
using PLang.Runtime2.modules.signing;

namespace PLang.Runtime2.modules.http;

[Action("download", Cacheable = false)]
public partial class download : IContext
{
    public partial string Url { get; init; }
    public partial string SaveTo { get; init; }

    [Default(FileExists.Error)]
    public partial FileExists IfExists { get; init; }

    public partial Dictionary<string, object>? Headers { get; init; }

    [Default(30)]
    public partial int TimeoutInSec { get; init; }

    [Default(false)]
    public partial bool Unsigned { get; init; }

    public partial sign? SignOptions { get; init; }
    public partial GoalCall? OnProgress { get; init; }

    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IHttpProvider>();
        if (!provider.Success) return provider;
        return await provider.Value!.DownloadAsync(this);
    }
}
