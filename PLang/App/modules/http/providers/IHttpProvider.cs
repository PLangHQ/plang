using App.Variables;
using App.Providers;

namespace App.modules.http.providers;

/// <summary>
/// HTTP provider interface. Actions pass themselves — provider owns all behavior.
/// Swappable via app.Providers.
/// </summary>
public interface IHttpProvider : IProvider, IDisposable
{
    Task<Data> SendAsync(request action);
    Task<Data> DownloadAsync(download action);
    Task<Data> UploadAsync(upload action);
    Data Configure(configure action);
}
