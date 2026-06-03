using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// app/format/list/this.cs:415 / :446 — shape-based stamping (architect's
// 829785fbe revision). `type` names the data's *shape*; `kind` names the
// encoding within that shape. json/xml/yaml → `{object, kind}` (keeps
// today's json→object); csv/xlsx → the new `{table, kind}`. Grouping by
// shape is what lets a renderer draw a grid by dispatching on
// `type=table` alone.
public class FormatRemapTests
{
    // json keeps today's mapping — `{object, json}`, not `{text, json}`.
    // Stamping the type does NOT parse; raw stays the json string.
    [Test] public async Task TypeFromMime_ApplicationJson_ReturnsObjectJson() { throw new System.NotImplementedException("not implemented"); }

    // xml/yaml are also `object`-shaped (tree, navigated by key).
    [Test] public async Task TypeFromMime_ApplicationXml_ReturnsObjectXml() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task TypeFromExtension_DotJson_ReturnsObjectJson() { throw new System.NotImplementedException("not implemented"); }

    // The new `table` type — csv and xlsx are *grids* (rows/columns), not
    // trees. Grouping by shape: same type, different kind.
    [Test] public async Task TypeFromExtension_DotCsv_ReturnsTableCsv() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task TypeFromExtension_DotXlsx_ReturnsTableXlsx() { throw new System.NotImplementedException("not implemented"); }

    // Image stays image — only the structured-text mapping shifted.
    [Test] public async Task TypeFromExtension_DotPng_ReturnsImagePng() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task TypeFromMime_ApplicationOctetStream_StampsBytesNotObject() { throw new System.NotImplementedException("not implemented"); }

    // Independent #16 — the convergence pin. Both routes (.json extension
    // and application/json MIME) must produce the same stamp. Otherwise
    // file.read and http.get would land different shapes for the same
    // content type. Same probe for csv.
    [Test] public async Task TypeFromExtension_AgreesWith_TypeFromMime_ForDotJson() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task TypeFromExtension_AgreesWith_TypeFromMime_ForDotCsv() { throw new System.NotImplementedException("not implemented"); }
}
