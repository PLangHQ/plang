using app.error;
using app.variable;

namespace app.module.action.setting;

/// <summary>
/// Gets a settings value by key from the System actor's settings store.
/// PLang: get settings 'ApiKey', write to %apiKey%
/// </summary>
[Action("get")]
public partial class Get : IContext
{
    public partial data.@this<global::app.type.item.text.@this> Key { get; init; }

    public async Task<data.@this> Run()
    {
        var key = (await Key.Value())!.Clr<string>()!;
        var store = await Context.App.SettingsStore;
        var result = await store.Get<global::app.type.item.@this>(global::app.setting.@this.Table, key);

        if (!result.Success)
            return result;

        // A missing key resolves to the typed null/absent citizen, not C# null —
        // detect "no value" via the value's own emptiness, then ask for it.
        var value = await result.Value();
        if (value is null || await value.IsEmpty())
            return Context.Error(new AskError(
                $"Settings value '{key}' is not set. Please provide a value.",
                "settings", key));

        return result;
    }
}
