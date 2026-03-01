using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.settings;

/// <summary>
/// Sets a settings value by key in the System actor's DataSource.
/// PLang: set settings 'ApiKey' = 'sk-123...'
/// </summary>
[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    public partial string Key { get; init; }
    public partial object? Value { get; init; }

    public async Task<Data> Run()
    {
        var dataSource = Context.Engine.System.DataSource;
        var result = await dataSource.Set("settings", Key, Value);

        if (!result.Success)
            return result;

        return Data.Ok(new types.setting { key = Key, value = Value });
    }
}
