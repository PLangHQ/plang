using System.Text;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
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
        app.actor.list.User.Channel.Register(ch);
        return ch;
    }

    [Test] public async Task ChannelRead_StampsTypeKind_FromMime()
    {
        await using var app = NewApp();
        var ch = Input(app, "application/json", Encoding.UTF8.GetBytes("{\"port\":8080}"));
        var d = await ch.Read();
        // The MIME is a format: a kind of the type that reads it — json is item's.
        await Assert.That(d.Type.Name).IsEqualTo("item");
        await Assert.That(d.Type.kind.Name).IsEqualTo("json");
    }

    [Test] public async Task ChannelRead_ProducesLazyData_RawSetValueNull()
    {
        await using var app = NewApp();
        var ch = Input(app, "application/json", Encoding.UTF8.GetBytes("{\"port\":8080}"));
        var d = await ch.Read();
        await Assert.That(d.HasRaw).IsTrue();
        await Assert.That(d.MaterializeCount()).IsEqualTo(0); // not parsed at read
    }

    // channel.this.cs defaults Mime to "text/plain" — text's own format: plain text is {text},
    // no kind; never a throw on an unset Mime.
    [Test] public async Task ChannelRead_DefaultTextPlainMime_StampsText()
    {
        await using var app = NewApp();
        var ch = Input(app, "text/plain", Encoding.UTF8.GetBytes("hello"));
        var d = await ch.Read();
        await Assert.That(d.Type.Name).IsEqualTo("text");
        await Assert.That(d.Type.kind.IsEmpty).IsTrue();
    }

    // Independent #15 — an octet-stream Mime stamps `{binary, null}` (no decode
    // hint) *and* `_raw` is `byte[]`.
    // Bytes a bare binary owns have nothing to read: the body is born a binary holding them.
    [Test] public async Task ChannelRead_OctetStreamMime_IsBornBinaryHoldingTheBytes()
    {
        await using var app = NewApp();
        byte[] body = { 1, 2, 3, 4 };
        var ch = Input(app, "application/octet-stream", body);
        var d = await ch.Read();
        await Assert.That(d.Type.Name).IsEqualTo("binary");
        await Assert.That(d.Type.kind.IsEmpty).IsTrue();
        await Assert.That(d.Peek() is global::app.type.item.binary.@this).IsTrue();
        await Assert.That(((global::app.type.item.binary.@this)d.Peek()).Value).IsEquivalentTo(body);
    }

    // An application/json body stamps `{item, json}`. The key invariant: stamping does NOT
    // parse — raw stays the unparsed bytes.
    [Test] public async Task ChannelRead_ApplicationJsonBody_StampsItemJson_NoParseAtStamp()
    {
        await using var app = NewApp();
        const string json = "{\"port\":8080}";
        var ch = Input(app, "application/json", Encoding.UTF8.GetBytes(json));
        var d = await ch.Read();
        await Assert.That(d.Type.Name).IsEqualTo("item");
        await Assert.That(d.Type.kind.Name).IsEqualTo("json");
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
        // Content off I/O is raw bytes now — untouched raw is the byte[], not the string.
        await Assert.That(d.Raw is byte[]).IsTrue();
    }

    // text/csv lands `{table, csv}`. Same lazy invariant: raw is the unparsed
    // bytes, no parse at stamp time.
    [Test] public async Task ChannelRead_TextCsvBody_StampsTableCsv_NoParseAtStamp()
    {
        await using var app = NewApp();
        const string csv = "name,age\nAda,36\n";
        var ch = Input(app, "text/csv", Encoding.UTF8.GetBytes(csv));
        var d = await ch.Read();
        await Assert.That(d.Type.Name).IsEqualTo("table");
        await Assert.That(d.Type.kind.Name).IsEqualTo("csv");
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
        // Content off I/O is raw bytes now — untouched raw is the byte[], not the string.
        await Assert.That(d.Raw is byte[]).IsTrue();
    }

    // `application/plang` is the self-describing container; channel.read hands
    // the body to plang's own format, which reconstructs the Data.
    [Test] public async Task ChannelRead_ApplicationPlangBody_DelegatesToPlangSerializer_LazyContainer()
    {
        await using var app = NewApp();
        var plang = app.actor.list.User.Context.Format("application/plang");
        var wire = (await plang.Serialize(app.Ok("hello"), app.actor.list.User.Context).Value())!.Clr<string>()!;

        var ch = Input(app, "application/plang", Encoding.UTF8.GetBytes(wire));
        var d = await ch.Read();
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("hello");
    }

    // The stream channel produces Mime-stamped lazy Data — a json-mime read lands
    // `{item, json}`, not a bare `text` string.
    [Test] public async Task StreamChannel_NoLongerReturnsBareText()
    {
        await using var app = NewApp();
        var ch = Input(app, "application/json", Encoding.UTF8.GetBytes("{\"a\":1}"));
        var d = await ch.Read();
        await Assert.That(d.HasRaw).IsTrue();
        await Assert.That(d.Type.Name).IsEqualTo("item");
    }
}
