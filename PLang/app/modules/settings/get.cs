using app.errors;
using app.variables;

namespace app.modules.settings;

/// <summary>
/// Gets a settings value by key from the System actor's settings store.
/// PLang: get settings 'ApiKey', write to %apiKey%
/// </summary>
[Action("get")]
public partial class Get : IContext
{
    public partial data.@this<string> Key { get; init; }

    public async Task<data.@this> Run()
    {
        var store = Context.App.SettingsStore;
        var result = await store.Get("settings", Key.Value!);

        if (!result.Success)
            return result;

        if (result.Value == null)
            return global::app.data.@this.FromError(new AskError(
                $"Settings value '{Key.Value}' is not set. Please provide a value.",
                "settings", Key.Value!));

        return result;
    }
}
