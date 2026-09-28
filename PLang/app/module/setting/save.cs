namespace app.module.setting;

/// <summary>
/// Saves a setting whole as the asking actor's row — what it holds now (its defaults, a saved row, this
/// run's values), kept for the runs after this one.
/// PLang: save %!app.goal.list.setting%
/// </summary>
[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial data.@this<global::app.type.item.setting.@this> Setting { get; init; }

    /// <summary>Returns no value — a setting may hold secrets (an identity's keys), and a result would
    /// carry them onto %!data%, the debug output and the wire.</summary>
    public async Task<data.@this> Start()
        => await Context.Setting.Save((await Setting.Value())!) is { Success: false } failed ? failed : Context.Ok();
}
