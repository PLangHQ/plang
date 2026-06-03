using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// app/format/list/this.cs:415 / :446 — Decision (Part 1) remaps
// structured-text MIMEs to `{text, kind}` instead of today's `{object,
// kind}`. The Format.TypeFromMime / TypeFromExtension behaviour changes;
// these rows pin the new mapping at the format layer where channel.read
// reads it.
public class FormatRemapTests
{
    [Test] public async Task TypeFromMime_ApplicationJson_ReturnsTextJson_NotObjectJson() { throw new System.NotImplementedException("not implemented"); }

    // Forward-looking — if xml/yaml join the structured-text family.
    // The remap rule is the contract; whichever MIMEs are in the registry
    // at land-time should follow it.
    [Test] public async Task TypeFromMime_ApplicationXml_ReturnsTextXml() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task TypeFromExtension_DotJson_ReturnsTextJson() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task TypeFromExtension_DotCsv_ReturnsTextCsv() { throw new System.NotImplementedException("not implemented"); }

    // Image stays image — only the text family is being remapped.
    [Test] public async Task TypeFromExtension_DotPng_ReturnsImagePng() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task TypeFromMime_ApplicationOctetStream_StampsBytesNotObject() { throw new System.NotImplementedException("not implemented"); }

    // Independent #16 — the convergence pin. Both routes (.json extension
    // and application/json MIME) must produce the same stamp. Otherwise
    // file.read and http.get would land different shapes for the same
    // content type.
    [Test] public async Task TypeFromExtension_AgreesWith_TypeFromMime_ForDotJson() { throw new System.NotImplementedException("not implemented"); }
}
