using App.Variables;

namespace App.modules.settings;

/// <summary>
/// Removes a settings value by key from the System actor's DataSource.
/// PLang: remove settings 'ApiKey'
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial string Key { get; init; }

    public async Task<Data> Run()
    {
        var store = Context.App.System.SettingsStore;
        var result = await store.Remove("settings", Key);

        if (!result.Success)
            return result;

        return Data.Ok(new types.setting { key = Key });
    }
}
