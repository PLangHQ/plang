using app.variable;

namespace app.module.settings;

/// <summary>
/// Removes a settings value by key from the System actor's DataSource.
/// PLang: remove settings 'ApiKey'
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<global::app.type.text.@this> Key { get; init; }

    public async Task<data.@this<type.setting>> Run()
    {
        var key = (await Key.Value())!.Clr<string>()!;
        var store = await Context.App.SettingsStore;
        var result = await store.Remove("settings", key);

        if (!result.Success)
            return data.@this<type.setting>.From(result);

        return Context.Ok<type.setting>(new type.setting { key = key });
    }
}
