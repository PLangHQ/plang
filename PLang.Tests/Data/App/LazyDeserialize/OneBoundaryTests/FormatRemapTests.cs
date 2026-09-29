using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// app.type.list.Mime / app.type.list.Extension — the type content off I/O arrives as: the format, a
// kind of the type that reads it. The value stays unread until touched.
public class FormatRemapTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.type.list.@this Types => app.actor.list.User.Context.App.type.list;
    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    // json is item's kind: `{item, json}` — what a json value reports.
    [Test] public async Task Mime_ApplicationJson_ReturnsItemJson()
    {
        var t = Types.Stamp("application/json", Ctx);
        await Assert.That(t.Name).IsEqualTo("item");
        await Assert.That(t.kind.Name).IsEqualTo("json");
    }

    [Test] public async Task Mime_ApplicationXml_ReturnsTextXml()
    {
        var t = Types.Stamp("application/xml", Ctx);
        await Assert.That(t.Name).IsEqualTo("text");
        await Assert.That(t.kind.Name).IsEqualTo("xml");
    }

    [Test] public async Task Extension_DotJson_ReturnsItemJson()
    {
        var t = Types.Extension(".json", Ctx);
        await Assert.That(t.Name).IsEqualTo("item");
        await Assert.That(t.kind.Name).IsEqualTo("json");
    }

    // csv is table's — table reads it.
    [Test] public async Task Extension_DotCsv_ReturnsTableCsv()
    {
        var t = Types.Extension(".csv", Ctx);
        await Assert.That(t.Name).IsEqualTo("table");
        await Assert.That(t.kind.Name).IsEqualTo("csv");
    }

    // xlsx stays binary's until a type reads it.
    [Test] public async Task Extension_DotXlsx_ReturnsBinaryXlsx()
    {
        var t = Types.Extension(".xlsx", Ctx);
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.kind.Name).IsEqualTo("xlsx");
    }

    [Test] public async Task Extension_DotPng_ReturnsImagePng()
    {
        var t = Types.Extension(".png", Ctx);
        await Assert.That(t.Name).IsEqualTo("image");
        await Assert.That(t.kind.Name).IsEqualTo("png");
    }

    // octet-stream is genuinely opaque bytes → `{binary}`: binary's own format, no kind.
    [Test] public async Task Mime_ApplicationOctetStream_StampsBytesNullKind()
    {
        var t = Types.Stamp("application/octet-stream", Ctx);
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.kind.IsEmpty).IsTrue();
    }

    // Both routes (.json extension and application/json MIME) land the same stamp — file.read and
    // http.get agree for the same content. Same probe for csv.
    [Test] public async Task Extension_AgreesWith_Mime_ForDotJson()
    {
        var byExt = Types.Extension(".json", Ctx);
        var byMime = Types.Stamp("application/json", Ctx);
        await Assert.That(byExt.Name).IsEqualTo(byMime.Name);
        await Assert.That(byExt.kind.Name).IsEqualTo(byMime.kind.Name);
    }

    [Test] public async Task Extension_AgreesWith_Mime_ForDotCsv()
    {
        var byExt = Types.Extension(".csv", Ctx);
        var byMime = Types.Stamp("text/csv", Ctx);
        await Assert.That(byExt.Name).IsEqualTo(byMime.Name);
        await Assert.That(byExt.kind.Name).IsEqualTo(byMime.kind.Name);
    }
}
