using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TypeEntity = global::app.type.@this;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// The normalising `type.Create(name, kind?, strict?)` factory — the single
// entry point the LLM, build pipeline, and tests reach to construct a type
// value. Slash-tolerance lives here too: a single "text/markdown" must split
// to {name:text, kind:markdown} so the LLM's occasional slash-emission doesn't
// escape into the wire.
public class TypeFactoryTests
{
    [Test] public async Task Factory_NameKindStrict_CarriesAllThree()
    {
        var t = TypeEntity.Create("image", "gif", strict: true);
        await Assert.That(t.Name).IsEqualTo("image");
        await Assert.That(t.Kind).IsEqualTo("gif");
        await Assert.That(t.Strict).IsTrue();
    }

    [Test] public async Task Factory_String_CanonicalisesNameToText()
    {
        // Stage 2 lands string→text canonicalisation; Stage 1 wires the
        // canonicalisation hook through the primitive alias table — currently
        // "string" stays "string" (alias table identity), so until Stage 2
        // flips primitive.Canonical[typeof(string)] from "string" to "text",
        // the factory hands back the same name it received.
        var t = TypeEntity.Create("string");
        // Stage 2 will change this to "text"; Stage 1 pins the identity path.
        await Assert.That(t.Name).IsEqualTo("string");
    }

    [Test] public async Task Factory_SingleStringWithSlash_SplitsToNameAndKind()
    {
        var t = TypeEntity.Create("text/markdown");
        await Assert.That(t.Name).IsEqualTo("text");
        await Assert.That(t.Kind).IsEqualTo("markdown");
    }

    [Test] public async Task Factory_SingleStringNoSlash_KindIsNull()
    {
        var t = TypeEntity.Create("text");
        await Assert.That(t.Name).IsEqualTo("text");
        await Assert.That(t.Kind).IsNull();
    }

    [Test] public async Task Factory_MultiSlash_SplitsOnFirst()
    {
        // First slash splits; the rest is the (free-string) kind, not an error.
        var t = TypeEntity.Create("a/b/c");
        await Assert.That(t.Name).IsEqualTo("a");
        await Assert.That(t.Kind).IsEqualTo("b/c");
    }

    [Test] public async Task Factory_StrictDefaultsFalse()
    {
        var a = TypeEntity.Create("text");
        var b = TypeEntity.Create("text", "md");
        await Assert.That(a.Strict).IsFalse();
        await Assert.That(b.Strict).IsFalse();
    }

    [Test] public async Task Factory_CaseInsensitiveName()
    {
        // primitive.Aliases is OrdinalIgnoreCase. Pinned: factory lowercases
        // through the canonicaliser so unknown-but-aliased names resolve.
        var a = TypeEntity.Create("Text");
        var b = TypeEntity.Create("TEXT");
        await Assert.That(a.Name).IsEqualTo("text");
        await Assert.That(b.Name).IsEqualTo("text");
    }

    [Test] public async Task Factory_EmptyName_Rejected()
    {
        await Assert.That(() => TypeEntity.Create("")).Throws<System.ArgumentException>();
        await Assert.That(() => TypeEntity.Create("   ")).Throws<System.ArgumentException>();
    }

    [Test] public async Task Factory_NullSentinel_NameKindStrictPreserved()
    {
        await Assert.That(TypeEntity.Null.Name).IsEqualTo("null");
        await Assert.That(TypeEntity.Null.Kind).IsNull();
        await Assert.That(TypeEntity.Null.Strict).IsFalse();
    }

    [Test] public async Task Factory_StrictTrueOnTextFamily_NoThrowAtConstruction()
    {
        // strict on a family without IKindValidatable degrades to "kind-name-
        // accepted" — construction never throws; the byte-sniff path simply
        // never runs. Pinned: factory does not vet against the marker.
        var t = TypeEntity.Create("text", "md", strict: true);
        await Assert.That(t.Strict).IsTrue();
        await Assert.That(t.Name).IsEqualTo("text");
        await Assert.That(t.Kind).IsEqualTo("md");
    }
}
