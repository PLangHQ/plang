using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Settings;

/// <summary>
/// Specialized Data that lazily loads settings from the System actor's DataSource.
/// Registered on MemoryStack as "Settings" so that %Settings.ApiKey% resolves to a per-key database read.
/// Overrides GetChild to intercept navigation and load from DataSource instead of navigating Value.
/// </summary>
public class SettingsData : Data
{
    private const string SettingsTable = "settings";
    private readonly Engine.@this _engine;

    public SettingsData(Engine.@this engine)
        : base("Settings", null)
    {
        _engine = engine;
    }

    /// <summary>
    /// Intercepts dot-notation navigation. When PLang resolves %Settings.ApiKey%,
    /// MemoryStack.Get("Settings") returns this object, then calls GetChild("ApiKey").
    /// We load the value from DataSource instead of navigating an in-memory Value.
    /// </summary>
    public override Data? GetChild(string path, int depth = 0)
    {
        if (string.IsNullOrEmpty(path))
            return this;

        // Split path: "ApiKey.SubProp" → key="ApiKey", remaining="SubProp"
        var dotIndex = path.IndexOf('.');
        string key;
        string? remaining;

        if (dotIndex >= 0)
        {
            key = path[..dotIndex];
            remaining = path[(dotIndex + 1)..];
        }
        else
        {
            key = path;
            remaining = null;
        }

        // Load the value from the System actor's DataSource
        // .GetAwaiter().GetResult() is safe here because Microsoft.Data.Sqlite
        // is synchronous under the hood — SQLite has no async I/O.
        var store = _engine.System.SettingsStore;
        var result = store.Get(SettingsTable, key).GetAwaiter().GetResult();

        if (!result.Success)
            return result;

        if (result.Value == null)
        {
            // Key not found — return AskError so runtime can prompt the user
            return FromError(new AskError(
                $"Settings value '{key}' is not set. Please provide a value.",
                SettingsTable, key));
        }

        var child = new Data(key, result.Value, parent: this);
        child.Context = Context;

        if (string.IsNullOrEmpty(remaining))
            return child;

        // Navigate further into the loaded value (e.g., Settings.Config.SubKey)
        return child.GetChild(remaining, depth + 1);
    }
}
