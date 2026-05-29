using app.variable;
using app.modules.code;

namespace app.modules.http.code;

/// <summary>
/// HTTP provider interface. Actions pass themselves — provider owns all behavior.
/// Swappable via app.Code.
/// </summary>
public interface IHttp : ICode, IDisposable
{
    Task<data.@this> SendAsync(request action);
    Task<data.@this> DownloadAsync(download action);
    Task<data.@this> UploadAsync(upload action);
    data.@this Configure(configure action);
}
