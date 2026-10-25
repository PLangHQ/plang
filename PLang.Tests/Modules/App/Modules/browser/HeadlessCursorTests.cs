namespace PLang.Tests.App.Modules.browser;

/// <summary>
/// The cursor a headless browser sends up beside its frames is a text line on the frames' protocol, written as text —
/// one compact line whatever json's layout — and the window finds the cursor in it the way it reads every line.
/// </summary>
public class HeadlessCursorTests
{
    // the cursor's line, the one headless sends
    private static async Task<string> Line(global::app.@this app)
    {
        var line = await new global::app.module.browser.type.cursor.@this("pointer").Line(app.actor.list.User.Context);
        await line.IsSuccess();
        return (await line.Value())!.ToString();
    }

    [Test]
    public async Task TheCursor_IsOneCompactLine_WithJsonFormatted()
    {
        await using var app = new global::app.@this("/test", wiring: global::app.Wiring.none).Testing();

        await Assert.That(await Line(app)).IsEqualTo("{\"cursor\":\"pointer\"}");
    }

    [Test]
    public async Task TheWindow_FindsTheCursorInTheLine()
    {
        await using var app = new global::app.@this("/test", wiring: global::app.Wiring.none).Testing();

        await Assert.That(global::app.module.screen.type.screen.window.code.Window.Field(await Line(app), "cursor")).IsEqualTo("pointer");
    }
}
