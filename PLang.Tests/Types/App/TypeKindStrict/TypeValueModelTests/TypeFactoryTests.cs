using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TypeEntity = global::app.type.@this;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// `app.type.list[...]` is the one door for a type by name: it takes the spelled forms ("string",
// any case) and hands back the canonical type. The identity door
// `app.type.list[type]` carries name, kind and strict through, the kind canonicalised.
public class TypeFactoryTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/tmp/typedoor-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test] public async Task Door_NameKindStrict_CarriesAllThree()
    {
        var t = _app.type.list[new TypeEntity("image", "gif", strict: true), _app.actor.list.User.Context];
        await Assert.That(t.Name).IsEqualTo("image");
        await Assert.That(t.kind.Name).IsEqualTo("gif");
        await Assert.That(t.Strict).IsTrue();
    }

    [Test] public async Task Door_String_CanonicalisesNameToText()
    {
        var t = _app.type.list["string"];
        await Assert.That(t.Name).IsEqualTo("text");
    }

    [Test] public async Task Door_Identity_CanonicalisesKind()
    {
        var t = _app.type.list[new TypeEntity("text", "markdown"), _app.actor.list.User.Context];
        await Assert.That(t.Name).IsEqualTo("text");
        await Assert.That(t.kind.Name).IsEqualTo("md");
    }

    [Test] public async Task Door_SingleStringNoSlash_KindIsNull()
    {
        var t = _app.type.list["text"];
        await Assert.That(t.Name).IsEqualTo("text");
        await Assert.That(t.kind.IsEmpty).IsTrue();
    }

    [Test] public async Task Door_StrictDefaultsFalse()
    {
        var a = _app.type.list["text"];
        var b = _app.type.list[new TypeEntity("text", "md"), _app.actor.list.User.Context];
        await Assert.That(a.Strict).IsFalse();
        await Assert.That(b.Strict).IsFalse();
    }

    [Test] public async Task Door_CaseInsensitiveName()
    {
        var a = _app.type.list["Text"];
        var b = _app.type.list["TEXT"];
        await Assert.That(a.Name).IsEqualTo("text");
        await Assert.That(b.Name).IsEqualTo("text");
    }

    [Test] public async Task Door_EmptyName_Rejected()
    {
        await Assert.That(() => _app.type.list[""]).Throws<KeyNotFoundException>();
        await Assert.That(() => _app.type.list["   "]).Throws<KeyNotFoundException>();
    }

    [Test] public async Task Door_SameIdentity_SameFullType()
    {
        var a = _app.type.list[new TypeEntity("image", "gif"), _app.actor.list.User.Context];
        var b = _app.type.list[new TypeEntity("image", "gif"), _app.actor.list.User.Context];
        await Assert.That(a.Equals(b)).IsTrue();
    }

    [Test] public async Task NullSentinel_NameKindStrictPreserved()
    {
        await Assert.That(TypeEntity.Null.Name).IsEqualTo("null");
        await Assert.That(TypeEntity.Null.kind.IsEmpty).IsTrue();
        await Assert.That(TypeEntity.Null.Strict).IsFalse();
    }

    [Test] public async Task Door_StrictTrueOnTextFamily_NoThrow()
    {
        // strict on a family without IKindValidatable degrades to "kind-name-accepted" —
        // the door never throws; the byte-sniff path simply never runs.
        var t = _app.type.list[new TypeEntity("text", "md", strict: true), _app.actor.list.User.Context];
        await Assert.That(t.Strict).IsTrue();
        await Assert.That(t.Name).IsEqualTo("text");
        await Assert.That(t.kind.Name).IsEqualTo("md");
    }
}
