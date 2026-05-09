using App.Variables;
using App.Code;

namespace App.modules.http.code;

/// <summary>
/// HTTP provider interface. Actions pass themselves — provider owns all behavior.
/// Swappable via app.Code.
/// </summary>
public interface IHttp : ICode, IDisposable
{
    Task<Data.@this> SendAsync(request action);
    Task<Data.@this> DownloadAsync(download action);
    Task<Data.@this> UploadAsync(upload action);
    Data.@this Configure(configure action);
}
