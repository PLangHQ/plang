using App.Variables;

namespace App.modules.settings;

/// <summary>
/// Removes a settings value by key from the System actor's DataSource.
/// PLang: remove settings 'ApiKey'
/// </summary>
[System.ComponentModel.Description("Delete a settings entry by Key from the System actor's persistent settings store")]
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial Data.@this<string> Key { get; init; }

    public async Task<Data.@this> Run()
    {
        var store = Context.App.SettingsStore;
        var result = await store.Remove("settings", Key.Value!);

        if (!result.Success)
            return result;

        return Data(new types.setting { key = Key.Value! });
    }
}
