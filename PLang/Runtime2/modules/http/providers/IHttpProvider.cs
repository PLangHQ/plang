using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.http.providers;

/// <summary>
/// HTTP provider interface. Actions pass themselves — provider owns all behavior.
/// Swappable via engine.Providers. Custom implementations can use HttpHelper utilities.
/// </summary>
public interface IHttpProvider : IProvider, IDisposable
{
    Task<Data> SendAsync(request action);
    Task<Data> DownloadAsync(download action);
    Task<Data> UploadAsync(upload action);
    Data Configure(configure action);
}
