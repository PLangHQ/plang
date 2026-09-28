namespace app.module.action.code;

/// <summary>
/// Marker interface for runtime-overridable C# implementations. The fields
/// support the developer-DLL-registration flow: <c>- code.load 'foo.dll'</c>
/// populates Name, IsBuiltIn, Source on the registered ICode. Which provider is an
/// interface's default is the registry's selection, not the provider's — one provider
/// can serve several interfaces.
/// </summary>
public interface ICode
{
    string Name { get; }

    /// <summary>
    /// True when this implementation was registered by <see cref="@this.RegisterDefaults"/>
    /// at App boot. Such implementations are reconstructed on App boot — they don't appear
    /// in snapshots because the fresh App will already have them registered. Default false.
    /// </summary>
    bool IsBuiltIn { get; set; }

    /// <summary>
    /// Origin path of the implementation, used when capturing for snapshot.
    /// Set by <see cref="app.module.action.code.load"/> to the absolute DLL path; null for
    /// in-process registrations and built-in defaults. On Restore, a non-null Source means
    /// the DLL must be loaded; missing source is a referent-integrity hard error.
    /// </summary>
    string? Source { get; set; }
}
