using app.variable;

namespace app.module.settings;

/// <summary>
/// Removes a settings value by key from the System actor's DataSource.
/// PLang: remove settings 'ApiKey'
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<string> Key { get; init; }

    public async Task<data.@this<type.setting>> Run()
    {
        var store = Context.App.SettingsStore;
        var result = await store.Remove("settings", Key.Value!);

        if (!result.Success)
            return data.@this<type.setting>.From(result);

        return data.@this<type.setting>.Ok(new type.setting { key = Key.Value! });
    }
}
