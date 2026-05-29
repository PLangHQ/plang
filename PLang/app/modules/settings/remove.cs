using app.variable;

namespace app.modules.settings;

/// <summary>
/// Removes a settings value by key from the System actor's DataSource.
/// PLang: remove settings 'ApiKey'
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<string> Key { get; init; }

    public async Task<data.@this<types.setting>> Run()
    {
        var store = Context.App.SettingsStore;
        var result = await store.Remove("settings", Key.Value!);

        if (!result.Success)
            return data.@this<types.setting>.From(result);

        return data.@this<types.setting>.Ok(new types.setting { key = Key.Value! });
    }
}
