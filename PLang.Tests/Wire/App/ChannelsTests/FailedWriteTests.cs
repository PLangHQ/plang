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

    // Written to a plang channel and read back through another app, the error comes back whole — its id, message,
    // key, status and its causes — and the read is that failure: the step that reads it fails, its on error runs.
    [Test]
    public async Task AFailedResult_ReadBackThroughAnotherApp_IsTheSameFailure()
    {
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false).Testing();
        var user = app.actor.list.User.Context;
        var capture = new MemoryStream();
        var output = new global::app.channel.type.stream.@this("out", capture,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "application/plang" };
        app.actor.list.User.Channel.Register(output);
        var sent = new global::app.error.Error("the disk is full", "DiskFull", 507)
            { list = { new global::app.error.Error("quota reached", "Quota", 507) } };
        await output.WriteAsync(user.Error(sent));

        await using var reader = new global::app.@this("/test2", autoWireConsoleChannels: false).Testing();
        var input = new global::app.channel.type.stream.@this("in", new MemoryStream(capture.ToArray()),
            global::app.channel.ChannelDirection.Input, ownsStream: true) { Mime = "application/plang" };
        reader.actor.list.User.Channel.Register(input);
        var read = await input.ReadAsync();

        await read.IsFailure();
        var back = read.Error;
        await Assert.That(back).IsNotNull();
        await Assert.That(back!.Id).IsEqualTo(sent.Id);
        await Assert.That(back.Message).IsEqualTo("the disk is full");
        await Assert.That(back.Key).IsEqualTo("DiskFull");
        await Assert.That(back.Status.Code.ToInt32()).IsEqualTo(507);
        await Assert.That(back.list.Single().Key).IsEqualTo("Quota");
    }

    // Between two apps signing for real: the failure is signed with its error, verified on arrival, and read
    // as that failure.
    [Test]
    public async Task AFailedResult_SignedBetweenActors_ArrivesAsTheSameFailure()
    {
        var root = Path.Combine(Path.GetTempPath(), "plang_failed_" + Guid.NewGuid().ToString("N")[..8]);
        await using var sender = new global::app.@this(Path.Combine(root, "sender"), autoWireConsoleChannels: false);
        await using var receiver = new global::app.@this(Path.Combine(root, "receiver"), autoWireConsoleChannels: false);
        var from = sender.actor.list.User.Context;
        var to = receiver.actor.list.User.Context;

        using var body = new MemoryStream();
        var encoded = await from.Format("application/plang").Encode(body,
            from.Error(new global::app.error.Error("the disk is full", "DiskFull", 507)), from);
        await encoded.IsSuccess();

        var read = await receiver.type.list["wire"].kind["plang"]!.Decode(body.ToArray(), to);

        await read.IsFailure();
        await Assert.That(read.Error!.Key).IsEqualTo("DiskFull");
        await Assert.That(read.Error.Message).IsEqualTo("the disk is full");
    }

    [Test]
    public async Task AFailedResult_ToAPlangChannel_CarriesTheError()
    {
        var plang = await Written("application/plang", framed: false);

        await Assert.That(plang).Contains("the disk is full");
        await Assert.That(plang).Contains("DiskFull");
    }
}
