namespace PLang.Tests.App.Serialization;

// A format's encode door takes the whole Data, and a stream channel hands its format the very Data it
// was asked to write — never just its value.
public class FormatEncodeContractTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/tmp/FormatEncodeContractTests-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task Encode_AcceptsData_ReturnsData()
    {
        var ctx = app.actor.list.User.Context;
        using var ms = new MemoryStream();
        var result = await ctx.Format("application/json").Encode(ms, app.Ok("hello"), ctx);
        await Assert.That(result).IsNotNull();
        await result.IsSuccess();
    }

    [Test]
    public async Task StreamChannel_Write_HandsFullDataToTheFormat_NotValueOnly()
    {
        global::app.data.@this? last = null;
        app.type.list.Add(new global::app.type.kind.@this(
            new global::app.Attributes.FormatAttribute("xprobe", "application/x-probe", ".xprobe"), "binary",
            (stream, data, context, view, encoding, ct) => { last = data; return Task.FromResult(context.Ok()); }));

        var ch = new global::app.channel.type.stream.@this(
            "probe", new MemoryStream(), global::app.channel.ChannelDirection.Output)
        {
            Mime = "application/x-probe",
        };
        app.actor.list.User.Channel.Register(ch);

        var input = app.Ok("payload");
        await ch.WriteAsync(input);

        await Assert.That(last).IsNotNull();
        await Assert.That(ReferenceEquals(last, input)).IsTrue()
            .Because("Stream.Write must pass the same Data reference to the format.");
    }
}
