using app.Errors;

namespace app.Settings;

/// <summary>
/// Shared (one per app) settings collection. Holds Data values keyed by name,
/// backed by <see cref="app.@this.SettingsStore"/> for persistence. Registered
/// on every actor's <see cref="Variables.@this"/> via
/// <see cref="Variables.@this.RegisterNavigable"/> so <c>%Settings.X%</c>
/// resolution dispatches into <see cref="Get"/>.
/// </summary>
public sealed class @this
{
    private const string SettingsTable = "settings";
    private readonly app.@this _app;

    public @this(app.@this app) { _app = app; }

    /// <summary>
    /// Loads a setting by path. Path may be a single key ("ApiKey") or a
    /// dot-compound path ("ApiKey.SubProp"). Compound paths load the first
    /// segment from the store and navigate the result via Data.GetChild.
    /// Returns AskError when the value is unset.
    /// </summary>
    public Data.@this Get(string path, Actor.Context.@this context)
    {
        if (string.IsNullOrEmpty(path)) return Data.@this.NotFound("Settings");

        var dotIndex = path.IndexOf('.');
        var key = dotIndex >= 0 ? path[..dotIndex] : path;
        var remaining = dotIndex >= 0 ? path[(dotIndex + 1)..] : null;

        // .GetAwaiter().GetResult() is safe here because Microsoft.Data.Sqlite
        // is synchronous under the hood — SQLite has no async I/O.
        var result = _app.SettingsStore.Get(SettingsTable, key).GetAwaiter().GetResult();
        if (!result.Success) return result;

        if (result.Value == null)
            return Data.@this.FromError(new AskError(
                $"Settings value '{key}' is not set. Please provide a value.",
                SettingsTable, key));

        result.Context = context;

        return string.IsNullOrEmpty(remaining)
            ? result
            : result.GetChild(remaining);
    }

    /// <summary>Stores a Data value under the given key in the settings table.</summary>
    public Task<Data.@this> Set(string key, Data.@this value)
        => _app.SettingsStore.Set(SettingsTable, key, value);
}
