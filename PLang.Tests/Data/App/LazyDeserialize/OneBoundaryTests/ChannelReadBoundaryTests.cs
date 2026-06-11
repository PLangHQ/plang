using System.Text;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using stream = global::app.channel.type.stream.@this;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// Stage 4 — channel is the foundational I/O layer; there is one read verb,
// `channel.read`. Every read enters here. It stamps `type`/`kind` from the
// channel's `Mime` and produces lazy Data.
public class ChannelReadBoundaryTests
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-chan-" + System.Guid.NewGuid().ToString("N")[..8]));

    // Builds an input stream channel carrying `mime` over `body`, wired into the
    // actor's Channels (so the boundary can reach Format + context).
    private static stream Input(global::app.@this app, string mime, byte[] body)
    {
        var ch = new stream("c", new System.IO.MemoryStream(body), global::app.channel.ChannelDirection.Input) { Mime = mime };
        app.User.Channel.Register(ch);
        return ch;
    }

    [Test] public async Task ChannelRead_StampsTypeKind_FromMime()
    {
        await using var app = NewApp();
        var ch = Input(app, "application/json", Encoding.UTF8.GetBytes("{\"port\":8080}"));
        var d = await ch.Read();
        await Assert.That(d.Type.Name).IsEqualTo("item");
        await Assert.That(d.Type.Kind).IsEqualTo("json");
    }

    [Test] public async Task ChannelRead_ProducesLazyData_RawSetValueNull()
    {
        await using var app = NewApp();
        var ch = Input(app, "application/json", Encoding.UTF8.GetBytes("{\"port\":8080}"));
        var d = await ch.Read();
        await Assert.That(d.HasRaw).IsTrue();
        await Assert.That(d.MaterializeCount()).IsEqualTo(0); // not parsed at read
    }

    // Independent #14 — channel.this.cs defaults Mime to "text/plain". The
    // default stamp must be `{text, null}` — careless implementations could
    // stamp `{object, null}` or throw on an unset Mime.
    [Test] public async Task ChannelRead_DefaultTextPlainMime_StampsTextNullKind()
    {
        await using var app = NewApp();
        var ch = Input(app, "text/plain", Encoding.UTF8.GetBytes("hello"));
        var d = await ch.Read();
        await Assert.That(d.Type.Name).IsEqualTo("text");
        await Assert.That(d.Type.Kind).IsNull();
    }

    // Independent #15 — Decision 3 says raw is bytes only where genuinely
    // bytes. An octet-stream Mime stamps `{bytes, null}` *and* `_raw` is
    // `byte[]`, not `string`.
    [Test] public async Task ChannelRead_OctetStreamMime_StampsBytesAndRawIsByteArray()
    {
        await using var app = NewApp();
        byte[] body = { 1, 2, 3, 4 };
        var ch = Input(app, "application/octet-stream", body);
        var d = await ch.Read();
        await Assert.That(d.Type.Name).IsEqualTo("bytes");
        await Assert.That(d.Type.Kind).IsNull();
        await Assert.That(d.Raw is byte[]).IsTrue();
    }

    // Shape-based stamping (architect's 829785fbe revision) — an
    // application/json body stamps `{object, json}`. The key invariant:
    // stamping does NOT parse — `_raw` stays the json string.
    [Test] public async Task ChannelRead_ApplicationJsonBody_StampsObjectJson_NoParseAtStamp()
    {
        await using var app = NewApp();
        const string json = "{\"port\":8080}";
        var ch = Input(app, "application/json", Encoding.UTF8.GetBytes(json));
        var d = await ch.Read();
        await Assert.That(d.Type.Name).IsEqualTo("item");
        await Assert.That(d.Type.Kind).IsEqualTo("json");
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
        await Assert.That(d.Raw).IsEqualTo((object)json);
    }

    // text/csv lands `{table, csv}` — the new shape-based stamp. Same lazy
    // invariant: `_raw` is the csv string, no parse at stamp time.
    [Test] public async Task ChannelRead_TextCsvBody_StampsTableCsv_NoParseAtStamp()
    {
        await using var app = NewApp();
        const string csv = "name,age\nAda,36\n";
        var ch = Input(app, "text/csv", Encoding.UTF8.GetBytes(csv));
        var d = await ch.Read();
        await Assert.That(d.Type.Name).IsEqualTo("table");
        await Assert.That(d.Type.Kind).IsEqualTo("csv");
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
        await Assert.That(d.Raw).IsEqualTo((object)csv);
    }

    // `application/plang` is the self-describing container; channel.read hands
    // the body to the plang serializer, which reconstructs the Data.
    [Test] public async Task ChannelRead_ApplicationPlangBody_DelegatesToPlangSerializer_LazyContainer()
    {
        await using var app = NewApp();
        var serializer = app.User.Channel.Serializers.GetByMimeType("application/plang");
        var wire = (await serializer.Serialize(data.Ok("hello")).Value())!.Value;

        var ch = Input(app, "application/plang", Encoding.UTF8.GetBytes(wire));
        var d = await ch.Read();
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("hello");
    }

    // app/channel/stream/this.cs today returned bare text and ignored the
    // channel's Mime. After Stage 4 the stream channel produces Mime-stamped
    // lazy Data — a json-mime read lands `object`, not a bare `text` string.
    [Test] public async Task StreamChannel_NoLongerReturnsBareText()
    {
        await using var app = NewApp();
        var ch = Input(app, "application/json", Encoding.UTF8.GetBytes("{\"a\":1}"));
        var d = await ch.Read();
        await Assert.That(d.HasRaw).IsTrue();
        await Assert.That(d.Type.Name).IsEqualTo("item");
    }
}
