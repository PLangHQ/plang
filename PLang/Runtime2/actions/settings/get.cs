using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.settings;

/// <summary>
/// Gets a settings value by key from the System actor's DataSource.
/// PLang: get settings 'ApiKey', write to %apiKey%
/// </summary>
[Action("get")]
public partial class Get : IContext
{
    public partial string Key { get; init; }

    public async Task<Data> Run()
    {
        var dataSource = Context.Engine.System.DataSource;
        var result = await dataSource.Get("settings", Key);

        if (!result.Success)
            return result;

        if (result.Value == null)
        {
            return Data.FromError(new AskError(
                $"Settings value '{Key}' is not set. Please provide a value.",
                "settings", Key));
        }

        return Data.Ok(result.Value);
    }
}
