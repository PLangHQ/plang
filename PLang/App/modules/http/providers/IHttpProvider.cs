using App.Variables;
using App.Providers;

namespace App.modules.http.providers;

/// <summary>
/// HTTP provider interface. Actions pass themselves — provider owns all behavior.
/// Swappable via app.Providers.
/// </summary>
public interface IHttpProvider : IProvider, IDisposable
{
    Task<Data.@this> SendAsync(request action);
    Task<Data.@this> DownloadAsync(download action);
    Task<Data.@this> UploadAsync(upload action);
    Data.@this Configure(configure action);
}
