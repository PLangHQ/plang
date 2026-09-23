using app.variable;

namespace app.module.action.setting;

/// <summary>
/// Removes a settings value by key from the System actor's DataSource.
/// PLang: remove settings 'ApiKey'
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<global::app.type.item.text.@this> Key { get; init; }

    public async Task<data.@this> Run()
    {
        var key = (await Key.Value())!.Clr<string>()!;
        var store = await Context.App.SettingsStore;
        var result = await store.Remove("settings", key);
        return result.Success ? Context.Ok() : result;
    }
}
