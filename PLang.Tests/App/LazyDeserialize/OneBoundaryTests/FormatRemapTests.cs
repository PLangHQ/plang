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
    // json keeps today's mapping — `{object, json}`, not `{text, json}`.
    [Test] public async Task TypeFromMime_ApplicationJson_ReturnsObjectJson()
    {
        var t = new format().TypeFromMime("application/json");
        await Assert.That(t.Name).IsEqualTo("object");
        await Assert.That(t.Kind).IsEqualTo("json");
    }

    // xml/yaml are also `object`-shaped (tree, navigated by key).
    [Test] public async Task TypeFromMime_ApplicationXml_ReturnsObjectXml()
    {
        var t = new format().TypeFromMime("application/xml");
        await Assert.That(t.Name).IsEqualTo("object");
        await Assert.That(t.Kind).IsEqualTo("xml");
    }

    [Test] public async Task TypeFromExtension_DotJson_ReturnsObjectJson()
    {
        var t = new format().TypeFromExtension(".json");
        await Assert.That(t.Name).IsEqualTo("object");
        await Assert.That(t.Kind).IsEqualTo("json");
    }

    // The new `table` type — csv and xlsx are *grids* (rows/columns), not
    // trees. Grouping by shape: same type, different kind.
    [Test] public async Task TypeFromExtension_DotCsv_ReturnsTableCsv()
    {
        var t = new format().TypeFromExtension(".csv");
        await Assert.That(t.Name).IsEqualTo("table");
        await Assert.That(t.Kind).IsEqualTo("csv");
    }

    [Test] public async Task TypeFromExtension_DotXlsx_ReturnsTableXlsx()
    {
        var t = new format().TypeFromExtension(".xlsx");
        await Assert.That(t.Name).IsEqualTo("table");
        await Assert.That(t.Kind).IsEqualTo("xlsx");
    }

    // Image stays image — only the structured-text/tabular mapping shifted.
    [Test] public async Task TypeFromExtension_DotPng_ReturnsImagePng()
    {
        var t = new format().TypeFromExtension(".png");
        await Assert.That(t.Name).IsEqualTo("image");
        await Assert.That(t.Kind).IsEqualTo("png");
    }

    // octet-stream names nothing here — it is genuinely opaque bytes and the
    // format registry returns the Null sentinel ("callers stamp nothing").
    // The bytes stamp is the channel boundary's job (see
    // ChannelReadBoundaryTests.OctetStream); critically it is NOT `object`.
    [Test] public async Task TypeFromMime_ApplicationOctetStream_StampsBytesNotObject()
    {
        var t = new format().TypeFromMime("application/octet-stream");
        await Assert.That(t.IsNull).IsTrue();
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
        await Assert.That(byExt.Kind).IsEqualTo(byMime.Kind);
    }

    [Test] public async Task TypeFromExtension_AgreesWith_TypeFromMime_ForDotCsv()
    {
        var f = new format();
        var byExt = f.TypeFromExtension(".csv");
        var byMime = f.TypeFromMime("text/csv");
        await Assert.That(byExt.Name).IsEqualTo(byMime.Name);
        await Assert.That(byExt.Kind).IsEqualTo(byMime.Kind);
    }
}
