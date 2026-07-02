using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.TypeKindStrict.KindDerivationTests;

public class KindCanonicalisationTests
{
    [Test] public async Task Canonicalise_Markdown_ToMd()
    {
        await using var app = TestApp.Create("/test");
        await Assert.That(app.Format.CanonicaliseKind("markdown")).IsEqualTo("md");
    }

    [Test] public async Task Canonicalise_Jpeg_ToJpg()
    {
        await using var app = TestApp.Create("/test");
        await Assert.That(app.Format.CanonicaliseKind("jpeg")).IsEqualTo("jpg");
    }

    [Test] public async Task Canonicalise_UnknownFrobnicate_PassesThrough()
    {
        await using var app = TestApp.Create("/test");
        await Assert.That(app.Format.CanonicaliseKind("frobnicate")).IsEqualTo("frobnicate");
    }

    [Test] public async Task Canonicalise_SharedSubtypePicksPrimary()
    {
        await using var app = TestApp.Create("/test");
        // Shared MIME subtype across two extensions: shorter extension wins.
        await Assert.That(app.Format.CanonicaliseKind("jpeg")).IsEqualTo("jpg");
    }

    [Test] public async Task Canonicalise_NullInput_ReturnsNull()
    {
        await using var app = TestApp.Create("/test");
        await Assert.That(app.Format.CanonicaliseKind(null)).IsNull();
    }

    [Test] public async Task Canonicalise_AliasTableDerived_NotHandWritten()
    {
        // Register a fresh extension at runtime; its MIME subtype should
        // canonicalise to the freshly-registered extension. Proves the table
        // is derived from the registry, not a literal map.
        await using var app = TestApp.Create("/test");
        app.Format.Add(".frobx", "frob-kind", "application/x-frobnicate");
        await Assert.That(app.Format.CanonicaliseKind("x-frobnicate")).IsEqualTo("frobx");
    }
}
