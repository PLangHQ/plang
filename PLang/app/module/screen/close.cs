using app.module.screen.code;

namespace app.module.screen;

/// <summary>Closes a screen (from <c>screen.open</c>).</summary>
[Action("close", Cacheable = false)]
public partial class close : IContext
{
    /// <summary>The screen, from <c>screen.open</c>.</summary>
    public partial data.@this<type.screen.@this> Screen { get; init; }

    public async Task<data.@this> Start()
    {
        (await Screen.Value())?.Close();
        return Context.Ok();
    }
}
