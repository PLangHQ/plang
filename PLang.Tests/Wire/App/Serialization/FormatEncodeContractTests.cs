namespace PLang.Tests.App.Serialization;

// A format's encode door takes the whole Data, and a stream channel hands its format the very Data it
// was asked to write — never just its value.
public class FormatEncodeContractTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.Session.App("/tmp/FormatEncodeContractTests-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
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
        var probe = new Probe(app.type.list["binary"]);
        app.type.list.Add(probe);

        var ch = new global::app.channel.type.stream.@this(
            "probe", new MemoryStream(), global::app.channel.ChannelDirection.Output)
        {
            Mime = "application/x-probe",
        };
        app.actor.list.User.Channel.Register(ch);

        var input = app.Ok("payload");
        await ch.WriteAsync(input);

        await Assert.That(probe.Last).IsNotNull();
        await Assert.That(ReferenceEquals(probe.Last, input)).IsTrue()
            .Because("Stream.Write must pass the same Data reference to the format.");
    }

    // A format that keeps the Data it was asked to write.
    private sealed class Probe(global::app.type.@this owner)
        : global::app.type.kind.@this(new global::app.Attributes.FormatAttribute("xprobe", "application/x-probe", ".xprobe"), owner)
    {
        public global::app.data.@this? Last { get; private set; }

        public override Task<global::app.data.@this> Encode(System.IO.Stream stream, global::app.data.@this data,
            global::app.actor.context.@this context, global::app.View? view = null, System.Text.Encoding? encoding = null,
            CancellationToken ct = default)
        {
            Last = data;
            return Task.FromResult(context.Ok());
        }
    }
}
