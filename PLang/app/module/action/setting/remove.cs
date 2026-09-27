namespace app.module.action.setting;

/// <summary>
/// Removes the asking actor's saved row for a setting — back to the system's row, or the defaults.
/// PLang: remove %!app.goal.list.setting%
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<global::app.type.item.setting.@this> Setting { get; init; }

    public async Task<data.@this> Start()
        => await Context.Setting.Remove((await Setting.Value())!) is { Success: false } failed ? failed : Context.Ok();
}
