using System.Text.Json.Serialization;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Settings;

/// <summary>
/// Data subclass for settings values.
///
/// Two construction modes:
/// - Runtime (context constructor): registered on MemoryStack as "Settings",
///   intercepts %Settings.ApiKey% via GetChild and loads from the settings store.
/// - Storage (JsonConstructor): value already loaded, used for store round-trips.
/// </summary>
public class SettingsData : Data
{
    private const string SettingsTable = "settings";
    private readonly Engine.@this? _engine;

    /// <summary>Runtime constructor — intercepts navigation and loads from settings store.</summary>
    public SettingsData(Engine.@this engine)
        : base("Settings", null)
    {
        _engine = engine;
    }

    /// <summary>Storage constructor — value already set, no lazy resolution needed.</summary>
    [JsonConstructor]
    public SettingsData(string name, object? value = null, Memory.Type? type = null)
        : base(name, value, type)
    {
    }

    /// <summary>
    /// Intercepts dot-notation navigation. When PLang resolves %Settings.ApiKey%,
    /// MemoryStack.Get("Settings") returns this object, then calls GetChild("ApiKey").
    /// We load the value from the settings store instead of navigating an in-memory Value.
    /// Only active for the runtime proxy (context constructor). Storage instances navigate normally.
    /// </summary>
    public override Data? GetChild(string path, int depth = 0)
    {
        if (_engine == null)
            return base.GetChild(path, depth);

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

        // .GetAwaiter().GetResult() is safe here because Microsoft.Data.Sqlite
        // is synchronous under the hood — SQLite has no async I/O.
        var store = _engine.System.SettingsStore;
        var result = store.Get<SettingsData>(SettingsTable, key).GetAwaiter().GetResult();

        if (!result.Success)
            return result;

        if (result.Value == null)
            return FromError(new AskError(
                $"Settings value '{key}' is not set. Please provide a value.",
                SettingsTable, key));

        result.Context = Context;

        if (string.IsNullOrEmpty(remaining))
            return result;

        return result.GetChild(remaining, depth + 1);
    }
}
