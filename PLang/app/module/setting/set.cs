using app.variable;

namespace app.module.setting;

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
        var key = (await Key.Value())!.Clr<string>()!;
        var val = Value == null ? null : await Value.Value();
        var store = await Context.App.SettingsStore;
        var result = await store.Set("settings", key, new data.@this(key, val, context: Context));

        if (!result.Success)
            return data.@this<type.setting>.From(result);

        return Context.Ok<type.setting>(new type.setting { key = key, value = val });
    }
}
