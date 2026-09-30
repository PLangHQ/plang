using app.module.screen.code;

namespace app.module.screen;

/// <summary>Closes a screen (from <c>screen.open</c>).</summary>
[Action("close", Cacheable = false)]
public partial class close : IContext
{
    /// <summary>The screen, from <c>screen.open</c>.</summary>
    public partial data.@this<Screen> Screen { get; init; }

    [Code]
    public partial IScreen Provider { get; }

    public async Task<data.@this> Start() => await Provider.Close(this);
}
