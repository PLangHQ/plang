namespace app.Code;

/// <summary>
/// Marker interface for runtime-overridable C# implementations. The fields
/// support the developer-DLL-registration flow: <c>- code.load 'foo.dll'</c>
/// populates Name, IsDefault, IsBuiltIn, Source on the registered ICode.
///
/// Renamed from ICode in stage 19 to align the runtime's vocabulary with
/// PLang's narrative: "everything is goals, except where you need code."
/// </summary>
public interface ICode
{
    string Name { get; }
    bool IsDefault { get; set; }

    /// <summary>
    /// True when this implementation was registered by <see cref="@this.RegisterDefaults"/>
    /// at App boot. Such implementations are reconstructed on App boot — they don't appear
    /// in snapshots because the fresh App will already have them registered. Default false.
    /// </summary>
    bool IsBuiltIn { get; set; }

    /// <summary>
    /// Origin path of the implementation, used when capturing for snapshot.
    /// Set by <see cref="app.modules.code.load"/> to the absolute DLL path; null for
    /// in-process registrations and built-in defaults. On Restore, a non-null Source means
    /// the DLL must be loaded; missing source is a referent-integrity hard error.
    /// </summary>
    string? Source { get; set; }
}
