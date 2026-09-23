using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TypeEntity = global::app.type.@this;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// `app.Type[...]` is the one door for a type by name: it takes the spelled forms ("string",
// "text/markdown", any case) and hands back the canonical type. The identity door
// `app.Type[type]` carries name, kind and strict through, the kind canonicalised.
public class TypeFactoryTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/tmp/typedoor-" + System.Guid.NewGuid().ToString("N")[..8]);

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test] public async Task Door_NameKindStrict_CarriesAllThree()
    {
        var t = _app.Type[new TypeEntity("image", "gif", strict: true)];
        await Assert.That(t.Name).IsEqualTo("image");
        await Assert.That(t.Kind?.Name).IsEqualTo("gif");
        await Assert.That(t.Strict).IsTrue();
    }

    [Test] public async Task Door_String_CanonicalisesNameToText()
    {
        var t = _app.Type["string"];
        await Assert.That(t.Name).IsEqualTo("text");
    }

    [Test] public async Task Door_SingleStringWithSlash_SplitsToNameAndCanonicalKind()
    {
        var t = _app.Type["text/markdown"];
        await Assert.That(t.Name).IsEqualTo("text");
        await Assert.That(t.Kind?.Name).IsEqualTo("md");
    }

    [Test] public async Task Door_SingleStringNoSlash_KindIsNull()
    {
        var t = _app.Type["text"];
        await Assert.That(t.Name).IsEqualTo("text");
        await Assert.That(t.Kind?.Name).IsNull();
    }

    [Test] public async Task Door_MultiSlash_SplitsOnFirst()
    {
        // First slash splits; the rest is the (free-string) kind, not an error.
        var t = _app.Type["a/b/c"];
        await Assert.That(t.Name).IsEqualTo("a");
        await Assert.That(t.Kind?.Name).IsEqualTo("b/c");
    }

    [Test] public async Task Door_StrictDefaultsFalse()
    {
        var a = _app.Type["text"];
        var b = _app.Type[new TypeEntity("text", "md")];
        await Assert.That(a.Strict).IsFalse();
        await Assert.That(b.Strict).IsFalse();
    }

    [Test] public async Task Door_CaseInsensitiveName()
    {
        var a = _app.Type["Text"];
        var b = _app.Type["TEXT"];
        await Assert.That(a.Name).IsEqualTo("text");
        await Assert.That(b.Name).IsEqualTo("text");
    }

    [Test] public async Task Door_EmptyName_Rejected()
    {
        await Assert.That(() => _app.Type[""]).Throws<KeyNotFoundException>();
        await Assert.That(() => _app.Type["   "]).Throws<KeyNotFoundException>();
    }

    [Test] public async Task Door_SameIdentity_SameFullType()
    {
        var a = _app.Type[new TypeEntity("image", "gif")];
        var b = _app.Type[new TypeEntity("image", "gif")];
        await Assert.That(ReferenceEquals(a, b)).IsTrue();
    }

    [Test] public async Task NullSentinel_NameKindStrictPreserved()
    {
        await Assert.That(TypeEntity.Null.Name).IsEqualTo("null");
        await Assert.That(TypeEntity.Null.Kind?.Name).IsNull();
        await Assert.That(TypeEntity.Null.Strict).IsFalse();
    }

    [Test] public async Task Door_StrictTrueOnTextFamily_NoThrow()
    {
        // strict on a family without IKindValidatable degrades to "kind-name-accepted" —
        // the door never throws; the byte-sniff path simply never runs.
        var t = _app.Type[new TypeEntity("text", "md", strict: true)];
        await Assert.That(t.Strict).IsTrue();
        await Assert.That(t.Name).IsEqualTo("text");
        await Assert.That(t.Kind?.Name).IsEqualTo("md");
    }
}
