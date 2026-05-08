using App.Errors;
using App.Variables;

namespace App.modules.settings;

/// <summary>
/// Gets a settings value by key from the System actor's settings store.
/// PLang: get settings 'ApiKey', write to %apiKey%
/// </summary>
[ModuleDescription("Persistent key-value settings store backed by the System actor's data source")]
[System.ComponentModel.Description("Retrieve a settings value by Key from the System actor's persistent settings store")]
[Action("get")]
public partial class Get : IContext
{
    public partial Data.@this<string> Key { get; init; }

    public async Task<Data.@this> Run()
    {
        var store = Context.App.SettingsStore;
        var result = await store.Get("settings", Key.Value!);

        if (!result.Success)
            return result;

        if (result.Value == null)
            return Error(new AskError(
                $"Settings value '{Key.Value}' is not set. Please provide a value.",
                "settings", Key.Value!));

        return result;
    }
}
