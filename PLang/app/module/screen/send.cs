using app.module.screen.code;

namespace app.module.screen;

/// <summary>
/// Gives a screen (PlangOS's, from <c>screen.open</c>) one line of JSON: an input event from the
/// host (<c>{"mouse":…}</c>, <c>{"key":…}</c>, <c>{"text":…}</c>, <c>{"clipboard":…}</c>) or a command
/// for one of its windows (<c>{"window":"focus|minimize|maximize|restore|close","id":…}</c>).
/// </summary>
[Action("send", Cacheable = false)]
public partial class send : IContext
{
    /// <summary>The event or command, one line of JSON (or a dict).</summary>
    public partial data.@this Data { get; init; }

    /// <summary>The screen, from <c>screen.open</c>.</summary>
    public partial data.@this<Screen> Screen { get; init; }

    [Code]
    public partial IScreen Provider { get; }

    public async Task<data.@this> Start() => await Provider.Send(this);
}
