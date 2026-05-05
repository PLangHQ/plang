namespace App.Providers;

/// <summary>
/// Base interface for all pluggable providers.
/// </summary>
public interface IProvider
{
    string Name { get; }
    bool IsDefault { get; set; }

    /// <summary>
    /// True when this provider was registered by <see cref="@this.RegisterDefaults"/>
    /// at App boot. Such providers are reconstructed on App boot — they don't appear
    /// in snapshots because the fresh App will already have them registered. Default false.
    /// </summary>
    bool IsBuiltIn { get; set; }

    /// <summary>
    /// Origin path of the provider's implementation, used when capturing for snapshot.
    /// Set by <see cref="App.modules.provider.load"/> to the absolute DLL path; null for
    /// in-process registrations and built-in defaults. On Restore, a non-null Source means
    /// the DLL must be loaded; missing source is a referent-integrity hard error.
    /// </summary>
    string? Source { get; set; }
}
