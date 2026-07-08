using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using format = global::app.format.list.@this;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// app/format/list/this.cs — shape-based stamping (architect's 829785fbe
// revision). `type` names the data's *shape*; `kind` names the encoding
// within that shape. json/xml/yaml → `{object, kind}` (keeps today's
// json→object); csv/xlsx → the new `{table, kind}`. Grouping by shape is
// what lets a renderer draw a grid by dispatching on `type=table` alone.
public class FormatRemapTests
{
    // The flip: content off I/O IS binary; the mime subtype is the decode hint
    // (the kind). json → `{binary, json}` — the kind narrows to a dict only on
    // Value() access, nothing is eagerly typed item here.
    [Test] public async Task TypeFromMime_ApplicationJson_ReturnsBinaryJson()
    {
        var t = new format().TypeFromMime("application/json");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsEqualTo("json");
    }

    // xml is also binary off the wire → `{binary, xml}`.
    [Test] public async Task TypeFromMime_ApplicationXml_ReturnsBinaryXml()
    {
        var t = new format().TypeFromMime("application/xml");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsEqualTo("xml");
    }

    [Test] public async Task TypeFromExtension_DotJson_ReturnsBinaryJson()
    {
        var t = new format().TypeFromExtension(".json");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsEqualTo("json");
    }

    // csv and xlsx are binary + the extension as kind; the kind narrows to a
    // table only on Value() access.
    [Test] public async Task TypeFromExtension_DotCsv_ReturnsBinaryCsv()
    {
        var t = new format().TypeFromExtension(".csv");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsEqualTo("csv");
    }

    [Test] public async Task TypeFromExtension_DotXlsx_ReturnsBinaryXlsx()
    {
        var t = new format().TypeFromExtension(".xlsx");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsEqualTo("xlsx");
    }

    // png is binary + png kind; it narrows to an image only on Value() access.
    [Test] public async Task TypeFromExtension_DotPng_ReturnsBinaryPng()
    {
        var t = new format().TypeFromExtension(".png");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsEqualTo("png");
    }

    // octet-stream is genuinely opaque bytes → `{binary, null}`: the binary
    // type with no decode hint. Not null, and critically not `object`.
    [Test] public async Task TypeFromMime_ApplicationOctetStream_StampsBytesNullKind()
    {
        var t = new format().TypeFromMime("application/octet-stream");
        await Assert.That(t.Name).IsEqualTo("binary");
        await Assert.That(t.Kind?.Name).IsNull();
        await Assert.That(t.Name).IsNotEqualTo("object");
    }

    // Independent #16 — the convergence pin. Both routes (.json extension
    // and application/json MIME) must produce the same stamp. Otherwise
    // file.read and http.get would land different shapes for the same
    // content type. Same probe for csv.
    [Test] public async Task TypeFromExtension_AgreesWith_TypeFromMime_ForDotJson()
    {
        var f = new format();
        var byExt = f.TypeFromExtension(".json");
        var byMime = f.TypeFromMime("application/json");
        await Assert.That(byExt.Name).IsEqualTo(byMime.Name);
        await Assert.That(byExt.Kind?.Name).IsEqualTo(byMime.Kind?.Name);
    }

    [Test] public async Task TypeFromExtension_AgreesWith_TypeFromMime_ForDotCsv()
    {
        var f = new format();
        var byExt = f.TypeFromExtension(".csv");
        var byMime = f.TypeFromMime("text/csv");
        await Assert.That(byExt.Name).IsEqualTo(byMime.Name);
        await Assert.That(byExt.Kind?.Name).IsEqualTo(byMime.Kind?.Name);
    }
}
