namespace PLang.Tests.App.ChannelsTests;

// A failed result written out writes what it holds: its error, in the error's own face — never an empty line.
public class FailedWriteTests
{
    private static async Task<string> Written(string mime, bool framed)
    {
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false).Testing();
        var user = app.actor.list.User.Context;
        var capture = new MemoryStream();
        var channel = new global::app.channel.type.stream.@this("out", capture,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = mime, Framed = framed };
        app.actor.list.User.Channel.Register(channel);

        var failed = user.Error(new global::app.error.Error("the disk is full", "DiskFull", 507));
        await channel.WriteAsync(failed);

        return System.Text.Encoding.UTF8.GetString(capture.ToArray());
    }

    [Test]
    public async Task AFailedResult_ToATextChannel_ShowsItsMessage()
    {
        var text = await Written("text/plain", framed: true);

        await Assert.That(text.Trim()).IsNotEmpty();
        await Assert.That(text).Contains("the disk is full");
    }

    [Test]
    public async Task AFailedResult_ToAPlangChannel_CarriesTheError()
    {
        var plang = await Written("application/plang", framed: false);

        await Assert.That(plang).Contains("the disk is full");
        await Assert.That(plang).Contains("DiskFull");
    }
}
