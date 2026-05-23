using app.variables;

namespace app.modules.settings;

/// <summary>
/// Sets a settings value by key in the System actor's settings store.
/// PLang: set settings 'ApiKey' = 'sk-123...'
/// </summary>
[System.ComponentModel.Description("Persist a key-value pair in the System actor's settings store")]
[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    public partial data.@this<string> Key { get; init; }
    public partial data.@this? Value { get; init; }

    public async Task<data.@this> Run()
    {
        var store = Context.App.SettingsStore;
        var result = await store.Set("settings", Key.Value!, new data.@this(Key.Value!, Value?.Value));

        if (!result.Success)
            return result;

        return global::app.data.@this.Ok(new types.setting { key = Key.Value!, value = Value?.Value });
    }
}
