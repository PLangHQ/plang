using app.error;

namespace app.module.settings;

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
    public async System.Threading.Tasks.ValueTask<data.@this> Get(string path, actor.context.@this context)
    {
        if (string.IsNullOrEmpty(path)) return data.@this.NotFound("Settings");

        var dotIndex = path.IndexOf('.');
        var key = dotIndex >= 0 ? path[..dotIndex] : path;
        var remaining = dotIndex >= 0 ? path[(dotIndex + 1)..] : null;

        var result = await _app.SettingsStore.Get(SettingsTable, key);
        if (!result.Success) return result;

        if (await result.Value() == null)
            return data.@this.FromError(new AskError(
                $"Settings value '{key}' is not set. Please provide a value.",
                SettingsTable, key));

        result.Context = context;

        return string.IsNullOrEmpty(remaining)
            ? result
            : await result.GetChild(remaining);
    }

    /// <summary>Stores a Data value under the given key in the settings table.</summary>
    public Task<data.@this> Set(string key, data.@this value)
        => _app.SettingsStore.Set(SettingsTable, key, value);
}
