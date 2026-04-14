using App.Errors;
using App.Variables;
using App.Settings;

namespace App.modules.settings;

/// <summary>
/// Gets a settings value by key from the System actor's settings store.
/// PLang: get settings 'ApiKey', write to %apiKey%
/// </summary>
[Action("get")]
public partial class Get : IContext
{
    public partial Data.@this<string> Key { get; init; }

    public async Task<Data.@this> Run()
    {
        var store = Context.App.System.SettingsStore;
        var result = await store.Get<SettingsVariable>("settings", Key.Value!);

        if (!result.Success)
            return result;

        if (result.Value == null)
            return Error(new AskError(
                $"Settings value '{Key.Value}' is not set. Please provide a value.",
                "settings", Key.Value!));

        return result;
    }
}
