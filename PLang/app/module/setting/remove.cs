namespace app.module.setting;

/// <summary>
/// Removes the asking actor's saved row for a setting — back to the system's row, or the defaults.
/// PLang: remove %!app.goal.list.setting%
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<global::app.type.item.setting.@this> Setting { get; init; }

    /// <summary>An actor's own setting is denied: its row is its owner's (an identity's row holds its key).</summary>
    public async Task<data.@this> Start()
    {
        var setting = (await Setting.Value())!;
        if (setting.Refusal is { } refused) return Context.Error(refused);
        return await Context.Setting.Remove(setting) is { Success: false } failed ? failed : Context.Ok();
    }
}
