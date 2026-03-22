using System.Net.Http;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.Engine.Settings;

namespace PLang.Runtime2.modules.http.providers;

/// <summary>
/// HTTP provider interface. Modules resolve via engine.Providers.Get&lt;IHttpProvider&gt;().
/// Tests and developers can swap implementations.
/// </summary>
public interface IHttpProvider : IProvider, IDisposable
{
    /// <summary>
    /// Sends an HTTP request. CompletionOption controls whether timeout applies to
    /// initial response (ResponseHeadersRead) or full response (ResponseContentRead).
    /// </summary>
    Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies configuration to the provider. Provider receives ISettings and casts to
    /// what it needs (OBP style). Returns Data — never throws.
    /// </summary>
    Data Configure(ISettings config);
}
