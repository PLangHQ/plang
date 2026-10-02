using app.module.screen.code;

namespace app.module.screen;

/// <summary>
/// Draws a frame in a screen (from <c>screen.open</c>): a JSON line <c>{"frame":"&lt;base64 jpeg or png&gt;",…}</c>
/// — what browser.start's OnFrame gives — or an image. Lines that aren't frames are ignored.
/// </summary>
[Action("draw", Cacheable = false)]
public partial class draw : IContext
{
    /// <summary>The frame: a JSON frame line, or an image.</summary>
    public partial data.@this Data { get; init; }

    /// <summary>The screen, from <c>screen.open</c>.</summary>
    public partial data.@this<type.screen.@this> Screen { get; init; }

    public async Task<data.@this> Start() => await Screen.Value() is { } screen ? await screen.Draw(Data, Context) : Context.Ok();
}
