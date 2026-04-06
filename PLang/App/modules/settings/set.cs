using App.Variables;
using App.Settings;

namespace App.modules.settings;

/// <summary>
/// Sets a settings value by key in the System actor's settings store.
/// PLang: set settings 'ApiKey' = 'sk-123...'
/// </summary>
[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    public partial string Key { get; init; }
    public partial object? Value { get; init; }

    public async Task<Data> Run()
    {
        var store = Context.App.System.SettingsStore;
        var result = await store.Set("settings", Key, new SettingsVariable(Key, Value));

        if (!result.Success)
            return result;

        return Data.Ok(new types.setting { key = Key, value = Value });
    }
}
