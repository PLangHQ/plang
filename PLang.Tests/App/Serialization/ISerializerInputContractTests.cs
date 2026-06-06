using System.Reflection;
using app.channel.serializer.list;
using app.channel.serializer;

namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 1
// ISerializer input tightened to Data. Coverage matrix rows 1.1, 1.2, 1.7, 1.8, 1.11.
// Architect refs: .bot/data-serialize-cleanup/architect/stage-1-iserializer-data.md,
//                 .bot/data-serialize-cleanup/architect/plan/test-coverage.md.

public class ISerializerInputContractTests
{
    // 1.1 — ISerializer.SerializeAsync(Stream, Data, ct) accepts Data and returns Task<Data>.
    [Test]
    public async Task SerializeAsync_AcceptsDataArgument_ReturnsTaskData()
    {
        var json = new global::app.channel.serializer.Json();
        using var ms = new MemoryStream();
        var result = await json.SerializeAsync(ms, global::app.data.@this.Ok("hello"));
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

    // 1.7 — SerializeOptions.Type carries the MIME string (renamed from ContentType).
    [Test]
    public async Task SerializeOptions_Type_CarriesMimeString()
    {
        var opts = new SerializeOptions { Type = "application/json" };
        await Assert.That(opts.Type).IsEqualTo("application/json");
    }

    // 1.8 — SerializeOptions.Data is typed as Data (not object?).
    [Test]
    public async Task SerializeOptions_Data_IsTypedAsData()
    {
        var prop = typeof(SerializeOptions).GetProperty("Data");
        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.PropertyType).IsEqualTo(typeof(global::app.data.@this));
    }

    // 1.11 — Stream channel's renamed Write hook passes the full Data into the registered
    //        serializer (not data.Value as before). Closes the "strip-then-rebuild" bug.
    [Test]
    public async Task StreamChannel_Write_HandsFullDataToSerializer_NotValueOnly()
    {
        var probe = new ProbeSerializer();

        await using var app = new global::app.@this("/tmp/stream-channel-write-test");
        var ch = new global::app.channel.type.stream.@this(
            "probe", new MemoryStream(), global::app.channel.ChannelDirection.Output)
        {
            Mime = probe.Type,
        };
        app.User.Channel.Register(ch);
        ch.Channels!.Serializers.Register(probe);

        var input = global::app.data.@this.Ok("payload");
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
        public Task<global::app.data.@this> SerializeAsync(Stream s, global::app.data.@this data, CancellationToken ct = default)
        {
            LastData = data;
            return Task.FromResult(global::app.data.@this.Ok());
        }
        public Task<global::app.data.@this> DeserializeAsync(Stream s, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());
        public Task<global::app.data.@this<T>> DeserializeAsync<T>(Stream s, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this<T>.Ok(default!));
        public global::app.data.@this<global::app.type.text.@this> Serialize(global::app.data.@this data) => global::app.data.@this<global::app.type.text.@this>.Ok("");
        public global::app.data.@this Deserialize(string s) => global::app.data.@this.Ok();
        public global::app.data.@this<T> Deserialize<T>(string s) => global::app.data.@this<T>.Ok(default!);
    }

    // SerializeOptions.Type-old: the previous ContentType property is gone.
    [Test]
    public async Task SerializeOptions_ContentType_PropertyRemoved()
    {
        var prop = typeof(SerializeOptions).GetProperty("ContentType");
        await Assert.That(prop).IsNull();
    }

    // DeserializeOptions / ResolveOptions: same Type rename — both compile in usage.
    [Test]
    public async Task DeserializeOptions_Type_CarriesMimeString_NoContentTypeProperty()
    {
        var opts = new DeserializeOptions { Type = "application/plang" };
        await Assert.That(opts.Type).IsEqualTo("application/plang");
        await Assert.That(typeof(DeserializeOptions).GetProperty("ContentType")).IsNull();
    }

    [Test]
    public async Task ResolveOptions_Type_CarriesMimeString_NoContentTypeProperty()
    {
        var opts = new ResolveOptions { Type = "application/plang" };
        await Assert.That(opts.Type).IsEqualTo("application/plang");
        await Assert.That(typeof(ResolveOptions).GetProperty("ContentType")).IsNull();
    }
}
