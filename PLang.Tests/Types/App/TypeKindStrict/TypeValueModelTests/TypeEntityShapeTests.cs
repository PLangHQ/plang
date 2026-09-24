using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TypeEntity = global::app.type.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// Public surface of `app.type.@this`. Name is the family (e.g. "image"); Kind
// is the subtype (e.g. "gif"); Strict is a bool; ClrType is non-public
// (interior callers reach it via the registry App.Type.Get/.Clr).
public class TypeEntityShapeTests
{
    [Test] public async Task Entity_HasName_NotValue()
    {
        var t = typeof(TypeEntity);
        await Assert.That(t.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)).IsNotNull();
        await Assert.That(t.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)).IsNull();
    }

    [Test] public async Task Entity_HasKindAndStrict_AsTopLevelMembers()
    {
        var t = typeof(TypeEntity);
        var kind = t.GetProperty("Kind", BindingFlags.Public | BindingFlags.Instance);
        var strict = t.GetProperty("Strict", BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(kind).IsNotNull();
        await Assert.That(kind!.PropertyType).IsEqualTo(typeof(global::app.type.kind.@this));
        await Assert.That(strict).IsNotNull();
        await Assert.That(strict!.PropertyType).IsEqualTo(typeof(bool));
    }

    [Test] public async Task Entity_ClrType_NotOnPublicSurface()
    {
        var t = typeof(TypeEntity);
        await Assert.That(t.GetProperty("ClrType", BindingFlags.Public | BindingFlags.Instance)).IsNull();
        // Interior access still works through the registry — App.Type.Clr(name).
        await using var app = TestApp.Create("/test");
        await Assert.That(app.Type.Clr("int")).IsEqualTo(typeof(global::app.type.item.number.@this));
    }

    [Test] public async Task Entity_FamilyKindAccessor_Removed()
    {
        // The old `type.Kind` that resolved via App.Format.FamilyOf(Value) is gone.
        // Pin: `type("image", null).Kind` reads null (no family-derivation), and
        // the door splits "image/jpeg" to {Name:"image", Kind:"jpg"} — Name
        // carries the family directly; Kind is the (canonical) subtype.
        var noSubtype = new TypeEntity("image");
        await Assert.That(noSubtype.Kind?.Name).IsNull();

        await using var app = TestApp.Create("/test");
        var split = app.Type["image/jpeg"];
        await Assert.That(split.Name).IsEqualTo("image");
        await Assert.That(split.Kind?.Name).IsEqualTo("jpg");
    }

    [Test] public async Task Entity_Kinds_PopulatedForNumber()
    {
        await using var app = TestApp.Create("/test");
        var num = app.Type["number"];
        await Assert.That(num.Kinds).IsNotNull();
        await Assert.That(num.Kinds!).Contains("int");
        await Assert.That(num.Kinds!).Contains("long");
        await Assert.That(num.Kinds!).Contains("decimal");
        await Assert.That(num.Kinds!).Contains("double");
    }

    [Test] public async Task Entity_Compressible_DerivesFromName()
    {
        await using var app = TestApp.Create("/test");
        // Compressibility is the format's knowledge about a type: image is already compressed.
        var image = app.Type["image"];
        await Assert.That(app.Format.Compressible(image)).IsFalse();
    }

    [Test] public async Task BareType_CarriesNoFacts_ItsFullTypeDoes()
    {
        // A bare type object is identity only; the facts live on its full type in app.type.
        await using var app = TestApp.Create("/test");
        var bare = new global::app.type.@this("identity");
        await Assert.That(bare.Property).IsNull();
        await Assert.That(app.Type[bare].Property).IsNotNull();
    }
}
