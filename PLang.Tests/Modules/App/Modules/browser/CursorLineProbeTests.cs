namespace PLang.Tests.App.Modules.browser;

/// <summary>Security probe (json-lines 477760b5d): the cursor comes from the page (untrusted); whatever it holds, the framed
/// channel writes exactly one line for it.</summary>
public class CursorLineProbeTests
{
    [Test]
    public async Task AHostileCursorValue_IsStillOneLine()
    {
        await using var app = new global::app.@this("/test", wiring: global::app.Wiring.none).Testing();
        var user = app.actor.list.User;
        var capture = new System.IO.MemoryStream();
        user.Channel.Register(new global::app.channel.type.stream.@this(user.Channel, "out", capture,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain", Framed = true });

        await user.Channel.Get("out")!.WriteAsync(new global::app.data.@this("!data",
            new global::app.module.browser.type.cursor.@this("x\r\n{\"cursor\":\"evil\"}\n" + (char)0x2028 + "y"), context: user.Context));

        var text = System.Text.Encoding.UTF8.GetString(capture.ToArray());
        await Assert.That(text.Split('\n').Length).IsEqualTo(2);
        await Assert.That(text).DoesNotContain("\r");
    }
}
