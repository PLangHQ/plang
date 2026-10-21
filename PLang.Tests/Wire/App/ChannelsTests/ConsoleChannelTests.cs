namespace PLang.Tests.App.ChannelsTests;

// The console writes and reads in the channel setting's format (%!app.channel.setting.format%): txt by default, for
// a person at a terminal; bin for the value's own bytes; plang's own for a program running this plang. The newline
// follows the format: formats written in lines get it, bytes nothing.
public class ConsoleChannelTests
{
    private static readonly byte[] Video = [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D];

    private static async Task<(global::app.@this App, MemoryStream Out)> Console(string? format)
    {
        var app = new global::app.@this("/test", autoWireConsoleChannels: false).Testing();
        if (format != null)
        {
            var set = app.actor.list.System.Setting.Set("app.channel.setting", new Dictionary<string, object?> { ["format"] = format });
            await set.IsSuccess();
        }
        var stream = new MemoryStream();
        app.actor.list.User.Channel.Register(new global::app.channel.type.console.@this(
            global::app.channel.list.@this.Output, stream, global::app.channel.ChannelDirection.Output));
        return (app, stream);
    }

    private static global::app.data.@this Mp4(global::app.@this app)
    {
        var context = app.actor.list.User.Context;
        var type = app.type.list.Of(app.type.list.Format("mp4")!, context);
        return context.Ok(new global::app.type.item.binary.@this(Video, type));
    }

    [Test]
    public async Task ByDefault_Bytes_AreTheirCount_AndANewline()
    {
        var (app, stream) = await Console(null);
        await using var _ = app;

        var written = await app.actor.list.User.Channel[global::app.channel.list.@this.Output].WriteAsync(Mp4(app));

        await written.IsSuccess();
        await Assert.That(System.Text.Encoding.UTF8.GetString(stream.ToArray()))
            .IsEqualTo($"[{Video.Length} bytes]{System.Environment.NewLine}");
    }

    [Test]
    public async Task InBin_AVideo_IsItsOwnBytes_WithNoNewline()
    {
        var (app, stream) = await Console("bin");
        await using var _ = app;

        var written = await app.actor.list.User.Channel[global::app.channel.list.@this.Output].WriteAsync(Mp4(app));

        await written.IsSuccess();
        await Assert.That(stream.ToArray()).IsEquivalentTo(Video);
    }

    [Test]
    public async Task InPlang_AValue_IsItsWholeData_OnALine()
    {
        var (app, stream) = await Console("plang");
        await using var _ = app;

        var written = await app.actor.list.User.Channel[global::app.channel.list.@this.Output]
            .WriteAsync(app.actor.list.User.Context.Ok("hello"));

        await written.IsSuccess();
        var text = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(text.EndsWith(System.Environment.NewLine)).IsTrue();
        await Assert.That(text.TrimStart().StartsWith('{')).IsTrue();
        await Assert.That(text).Contains("hello");
    }

    // A format is named as any format is (its name, MIME or extension); a name no format answers to is refused when set.
    [Test]
    public async Task AFormatNoTypeReads_IsRefusedWhenSet()
    {
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false).Testing();

        var set = await app.actor.list.System.Setting.Flag("app.channel.setting", new Dictionary<string, object?> { ["format"] = "nonsense" }, app.actor.list.System.Context);

        await set.IsFailure();
        await Assert.That(set.Error!.Key).IsEqualTo("FormatNotFound");
    }

    [Test]
    [Arguments("application/octet-stream")]
    [Arguments(".bin")]
    public async Task AFormat_IsNamedAsAnyFormatIs(string named)
    {
        var (app, stream) = await Console(named);
        await using var _ = app;

        await (await app.actor.list.User.Channel[global::app.channel.list.@this.Output].WriteAsync(Mp4(app))).IsSuccess();

        await Assert.That(stream.ToArray()).IsEquivalentTo(Video);
    }

    // An ask writes itself: in text its question, a line a person reads; in plang's own format the whole pending ask,
    // which a program running this plang reads as an ask.
    [Test]
    [Arguments(null)]
    [Arguments("plang")]
    public async Task AnAsk_GoesOutAsTheFormatShowsIt(string? format)
    {
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false).Testing();
        if (format != null)
            await app.actor.list.System.Setting.Set("app.channel.setting", new Dictionary<string, object?> { ["format"] = format }).IsSuccess();
        var context = app.actor.list.User.Context;
        var output = new MemoryStream();
        // the answer as the asker's format writes it: a line typed, or a whole Data from the program running this plang
        var input = new MemoryStream();
        if (format == null) input.Write(System.Text.Encoding.UTF8.GetBytes("Ada"));
        else await (await app.type.list.Format(format)!.Encode(input, context.Ok("Ada"), context)).IsSuccess();
        input.Write(System.Text.Encoding.UTF8.GetBytes("\n"));
        input.Position = 0;
        app.actor.list.User.Channel.Register(new global::app.channel.type.console.@this(
            global::app.channel.list.@this.Output, output, global::app.channel.ChannelDirection.Output));
        app.actor.list.User.Channel.Register(new global::app.channel.type.console.@this(
            global::app.channel.list.@this.Input, input, global::app.channel.ChannelDirection.Input));

        var ask = new global::app.module.output.ask(context)
        {
            Question = new global::app.data.@this<global::app.type.item.text.@this>("question", "what is your name?", context: context),
        };
        var answer = await new global::app.goal.step.action.@this(ask, context).Start(context);

        await answer.IsSuccess();
        await Assert.That((await answer.Value())?.ToString()).IsEqualTo("Ada");
        var asked = System.Text.Encoding.UTF8.GetString(output.ToArray());
        if (format == null)
            await Assert.That(asked).IsEqualTo($"what is your name?{System.Environment.NewLine}");
        else
        {
            await Assert.That(asked.TrimStart().StartsWith('{')).IsTrue();
            await Assert.That(asked).Contains("\"ask\"");
            await Assert.That(asked).Contains("what is your name?");
        }
    }
}
