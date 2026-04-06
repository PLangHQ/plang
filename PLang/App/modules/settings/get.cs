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
    public partial string Key { get; init; }

    public async Task<Data> Run()
    {
        var store = Context.App.System.SettingsStore;
        var result = await store.Get<SettingsVariable>("settings", Key);

        if (!result.Success)
            return result;

        if (result.Value == null)
            return Data.FromError(new AskError(
                $"Settings value '{Key}' is not set. Please provide a value.",
                "settings", Key));

        return result;
    }
}
