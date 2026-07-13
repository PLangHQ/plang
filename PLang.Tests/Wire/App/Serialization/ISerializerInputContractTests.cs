using System.Reflection;
using app.channel.serializer;

namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 1
// ISerializer input tightened to Data. Coverage matrix rows 1.1, 1.2, 1.7, 1.8, 1.11.
// Architect refs: .bot/data-serialize-cleanup/architect/stage-1-iserializer-data.md,
//                 .bot/data-serialize-cleanup/architect/plan/test-coverage.md.

public class ISerializerInputContractTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create("/tmp/ISerializerInputContractTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // 1.1 — ISerializer.SerializeAsync(Stream, Data, ct) accepts Data and returns Task<Data>.
    [Test]
    public async Task SerializeAsync_AcceptsDataArgument_ReturnsTaskData()
    {
        var json = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        using var ms = new MemoryStream();
        var result = await json.SerializeAsync(ms, app.Ok("hello"));
        await Assert.That(result).IsNotNull();
        await result.IsSuccess();
    }

    // 1.2 — Old polymorphic SerializeAsync(Stream, object, …) overload is gone.
    [Test]
    public async Task SerializeAsync_PolymorphicObjectOverload_NotPresentOnInterface()
    {
        var t = typeof(ISerializer);
        var methods = t.GetMethods().Where(m => m.Name == "SerializeAsync").ToList();
        await Assert.That(methods.Count).IsEqualTo(1);
        var parms = methods[0].GetParameters();
        await Assert.That(parms[1].ParameterType).IsEqualTo(typeof(global::app.data.@this));
    }

    // 1.11 — Stream channel's renamed Write hook passes the full Data into the registered
    //        serializer (not data.Value as before). Closes the "strip-then-rebuild" bug.
    [Test]
    public async Task StreamChannel_Write_HandsFullDataToSerializer_NotValueOnly()
    {
        var probe = new ProbeSerializer();

        await using var app = global::PLang.Tests.TestApp.Create("/tmp/stream-channel-write-test");
        var ch = new global::app.channel.type.stream.@this(
            "probe", new MemoryStream(), global::app.channel.ChannelDirection.Output)
        {
            Mime = probe.Type,
        };
        app.User.Channel.Register(ch);
        ch.Channels!.Serializers.Register(probe);

        var input = app.Ok("payload");
        await ch.WriteAsync(input);

        await Assert.That(probe.LastData).IsNotNull();
        await Assert.That(ReferenceEquals(probe.LastData, input)).IsTrue()
            .Because("Stream.Write must pass the same Data reference to the serializer.");
    }

    private sealed class ProbeSerializer : ISerializer
    {
        public global::app.data.@this? LastData { get; private set; }
        public string Type => "application/x-probe";
        public string Extension => ".probe";
        public Task<global::app.data.@this> SerializeAsync(Stream s, global::app.data.@this data, global::app.View view = global::app.View.Out, CancellationToken ct = default)
        {
            LastData = data;
            return Task.FromResult(global::app.data.@this.Ok());
        }
        public Task<global::app.data.@this> DeserializeAsync(Stream s, global::app.View view = global::app.View.Out, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());
        public Task<global::app.data.@this<T>> DeserializeAsync<T>(Stream s, global::app.View view = global::app.View.Out, CancellationToken ct = default) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
            => Task.FromResult(global::app.data.@this<T>.Ok(default!));
        public global::app.type.item.@this Read(global::app.type.item.source source, global::app.type.reader.ReadContext ctx) => global::app.type.item.@null.@this.Instance;
        public global::app.data.@this<global::app.type.item.text.@this> Serialize(global::app.data.@this data) => global::app.data.@this<global::app.type.item.text.@this>.Ok("");
        public global::app.data.@this Deserialize(string s) => global::app.data.@this.Ok();
        public global::app.data.@this<T> Deserialize<T>(string s) where T : global::app.type.item.@this, global::app.type.item.ICreate<T> => global::app.data.@this<T>.Ok(default!);
    }
}
