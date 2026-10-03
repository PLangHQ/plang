namespace PLang.Tests.App.ChannelsTests;

// One message leaves a channel whole: writers side by side (tasks writing out at once) never interleave within a
// line — each line is one message, its text and its frame.
public class WholeMessageTests
{
    // a stream that yields on every write, as a pipe or a console under load does, so writers can overlap
    private sealed class Yielding : MemoryStream
    {
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            await Task.Yield();
            await base.WriteAsync(buffer, offset, count, ct);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            await Task.Yield();
            await base.WriteAsync(buffer, ct);
        }

        public override void Write(byte[] buffer, int offset, int count) => base.Write(buffer, offset, count);
    }

    [Test]
    public async Task WritersSideBySide_EachLineIsOneWholeMessage()
    {
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false).Testing();
        var user = app.actor.list.User.Context;
        var capture = new Yielding();
        var channel = new global::app.channel.type.stream.@this("out", capture,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain", Framed = true };
        app.actor.list.User.Channel.Register(channel);

        var messages = Enumerable.Range(0, 40).Select(i => $"message number {i} written whole").ToList();
        await Task.WhenAll(messages.Select(message => Task.Run(() => channel.WriteAsync(user.Ok(message)))));

        var lines = System.Text.Encoding.UTF8.GetString(capture.ToArray())
            .Split(System.Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines.Order()).IsEquivalentTo(messages.Order());
    }
}
