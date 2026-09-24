using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// app.Type.Mime / app.Type.Extension — the type content off I/O arrives as:
// binary, with the MIME subtype / file extension as its kind.
public class FormatRemapTests
{
    private static global::app.type.list.@this Types => global::PLang.Tests.TestApp.SharedContext.App.Type;

    // The flip: content off I/O IS binary; the mime subtype is the decode hint
    // (the kind). json → `{binary, json}` — the kind narrows to a dict only on
    // Value() access, nothing is eagerly typed item here.
    [Test] public async Task Mime_ApplicationJson_ReturnsBinaryJson()
    {
        var t = Types.Mime("application/json");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsEqualTo("json");
    }

    // xml is also binary off the wire → `{binary, xml}`.
    [Test] public async Task Mime_ApplicationXml_ReturnsBinaryXml()
    {
        var t = Types.Mime("application/xml");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsEqualTo("xml");
    }

    [Test] public async Task Extension_DotJson_ReturnsBinaryJson()
    {
        var t = Types.Extension(".json");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsEqualTo("json");
    }

    // csv and xlsx are binary + the extension as kind; the kind narrows to a
    // table only on Value() access.
    [Test] public async Task Extension_DotCsv_ReturnsBinaryCsv()
    {
        var t = Types.Extension(".csv");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsEqualTo("csv");
    }

    [Test] public async Task Extension_DotXlsx_ReturnsBinaryXlsx()
    {
        var t = Types.Extension(".xlsx");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsEqualTo("xlsx");
    }

    // png is binary + png kind; it narrows to an image only on Value() access.
    [Test] public async Task Extension_DotPng_ReturnsBinaryPng()
    {
        var t = Types.Extension(".png");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsEqualTo("png");
    }

    // octet-stream is genuinely opaque bytes → `{binary, null}`: the binary
    // type with no decode hint. Not null, and critically not `object`.
    [Test] public async Task Mime_ApplicationOctetStream_StampsBytesNullKind()
    {
        var t = Types.Mime("application/octet-stream");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsNull();
        await Assert.That(t.Name).IsNotEqualTo("object");
    }

    // Independent #16 — the convergence pin. Both routes (.json extension
    // and application/json MIME) must produce the same stamp. Otherwise
    // file.read and http.get would land different shapes for the same
    // content type. Same probe for csv.
    [Test] public async Task Extension_AgreesWith_Mime_ForDotJson()
    {
        var byExt = Types.Extension(".json");
        var byMime = Types.Mime("application/json");
        await Assert.That(byExt.Name).IsEqualTo(byMime.Name);
        await Assert.That(byExt.Kind?.Name).IsEqualTo(byMime.Kind?.Name);
    }

    [Test] public async Task Extension_AgreesWith_Mime_ForDotCsv()
    {
        var byExt = Types.Extension(".csv");
        var byMime = Types.Mime("text/csv");
        await Assert.That(byExt.Name).IsEqualTo(byMime.Name);
        await Assert.That(byExt.Kind?.Name).IsEqualTo(byMime.Kind?.Name);
    }
}
