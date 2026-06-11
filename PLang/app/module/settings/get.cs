using app.error;
using app.variable;

namespace app.module.settings;

/// <summary>
/// Gets a settings value by key from the System actor's settings store.
/// PLang: get settings 'ApiKey', write to %apiKey%
/// </summary>
[Action("get")]
public partial class Get : IContext
{
    public partial data.@this<global::app.type.text.@this> Key { get; init; }

    public async Task<data.@this> Run()
    {
        var key = (await Key.Value())!.Clr<string>()!;
        var store = Context.App.SettingsStore;
        var result = await store.Get("settings", key);

        if (!result.Success)
            return result;

        if (await result.Value() == null)
            return global::app.data.@this.FromError(new AskError(
                $"Settings value '{key}' is not set. Please provide a value.",
                "settings", key));

        return result;
    }
}
