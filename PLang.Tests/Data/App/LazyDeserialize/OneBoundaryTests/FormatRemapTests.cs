using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// app.Type.Mime / app.Type.Extension — the type content off I/O arrives as:
// binary, with the MIME subtype / file extension as its kind.
public class FormatRemapTests
{
    private static global::app.type.list.@this Types => global::PLang.Tests.TestApp.SharedContext.App.Type;
    private static global::app.actor.context.@this Ctx => global::PLang.Tests.TestApp.SharedContext;

    // The flip: content off I/O IS binary; the mime subtype is the decode hint
    // (the kind). json → `{binary, json}` — the kind narrows to a dict only on
    // Value() access, nothing is eagerly typed item here.
    [Test] public async Task Mime_ApplicationJson_ReturnsBinaryJson()
    {
        var t = Types.Mime("application/json", Ctx);
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.kind.Name).IsEqualTo("json");
    }

    // xml is also binary off the wire → `{binary, xml}`.
    [Test] public async Task Mime_ApplicationXml_ReturnsBinaryXml()
    {
        var t = Types.Mime("application/xml", Ctx);
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.kind.Name).IsEqualTo("xml");
    }

    [Test] public async Task Extension_DotJson_ReturnsBinaryJson()
    {
        var t = Types.Extension(".json", Ctx);
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.kind.Name).IsEqualTo("json");
    }

    // csv and xlsx are binary + the extension as kind; the kind narrows to a
    // table only on Value() access.
    [Test] public async Task Extension_DotCsv_ReturnsBinaryCsv()
    {
        var t = Types.Extension(".csv", Ctx);
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.kind.Name).IsEqualTo("csv");
    }

    [Test] public async Task Extension_DotXlsx_ReturnsBinaryXlsx()
    {
        var t = Types.Extension(".xlsx", Ctx);
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.kind.Name).IsEqualTo("xlsx");
    }

    // png is binary + png kind; it narrows to an image only on Value() access.
    [Test] public async Task Extension_DotPng_ReturnsBinaryPng()
    {
        var t = Types.Extension(".png", Ctx);
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.kind.Name).IsEqualTo("png");
    }

    // octet-stream is genuinely opaque bytes → `{binary, null}`: the binary
    // type with no decode hint. Not null, and critically not `object`.
    [Test] public async Task Mime_ApplicationOctetStream_StampsBytesNullKind()
    {
        var t = Types.Mime("application/octet-stream", Ctx);
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.kind.IsEmpty).IsTrue();
        await Assert.That(t.Name).IsNotEqualTo("object");
    }

    // Independent #16 — the convergence pin. Both routes (.json extension
    // and application/json MIME) must produce the same stamp. Otherwise
    // file.read and http.get would land different shapes for the same
    // content type. Same probe for csv.
    [Test] public async Task Extension_AgreesWith_Mime_ForDotJson()
    {
        var byExt = Types.Extension(".json", Ctx);
        var byMime = Types.Mime("application/json", Ctx);
        await Assert.That(byExt.Name).IsEqualTo(byMime.Name);
        await Assert.That(byExt.kind.Name).IsEqualTo(byMime.kind.Name);
    }

    [Test] public async Task Extension_AgreesWith_Mime_ForDotCsv()
    {
        var byExt = Types.Extension(".csv", Ctx);
        var byMime = Types.Mime("text/csv", Ctx);
        await Assert.That(byExt.Name).IsEqualTo(byMime.Name);
        await Assert.That(byExt.kind.Name).IsEqualTo(byMime.kind.Name);
    }
}
