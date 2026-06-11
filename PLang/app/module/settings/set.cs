using app.variable;

namespace app.module.settings;

/// <summary>
/// Sets a settings value by key in the System actor's settings store.
/// PLang: set settings 'ApiKey' = 'sk-123...'
/// </summary>
[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    public partial data.@this<global::app.type.text.@this> Key { get; init; }
    public partial data.@this? Value { get; init; }

    public async Task<data.@this<type.setting>> Run()
    {
        var key = (await Key.Value())!.Value;
        var val = Value == null ? null : await Value.Value();
        var store = Context.App.SettingsStore;
        var result = await store.Set("settings", key, new data.@this(key, val));

        if (!result.Success)
            return data.@this<type.setting>.From(result);

        return data.@this<type.setting>.Ok(new type.setting { key = key, value = val });
    }
}
