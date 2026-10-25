namespace PLang.Tests.App.Modules.browser;

/// <summary>
/// The cursor a headless browser sends up beside its frames goes to OnFrame as itself; the channel OnFrame writes it to
/// makes it a line — one line, ended once by the channel's End.
/// </summary>
public class HeadlessCursorTests
{
    [Test]
    public async Task TheCursor_IsOneLine_EndedOnce()
    {
        await using var app = new global::app.@this("/test", wiring: global::app.Wiring.none).Testing();
        var user = app.actor.list.User;
        var capture = new System.IO.MemoryStream();
        user.Channel.Register(new global::app.channel.type.stream.@this(user.Channel, "out", capture,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain", Framed = true });

        await user.Channel.Get("out")!.WriteAsync(new global::app.data.@this("!data",
            new global::app.module.browser.type.cursor.@this("pointer"), context: user.Context));

        await Assert.That(System.Text.Encoding.UTF8.GetString(capture.ToArray())).IsEqualTo("{\"cursor\":\"pointer\"}\n");
    }
}
