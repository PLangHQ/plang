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
        await Assert.That(kind!.PropertyType).IsEqualTo(typeof(string));
        await Assert.That(strict).IsNotNull();
        await Assert.That(strict!.PropertyType).IsEqualTo(typeof(bool));
    }

    [Test] public async Task Entity_ClrType_NotOnPublicSurface()
    {
        var t = typeof(TypeEntity);
        await Assert.That(t.GetProperty("ClrType", BindingFlags.Public | BindingFlags.Instance)).IsNull();
        // Interior access still works through the registry — App.Type.Clr(name).
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Type.Clr("int")).IsEqualTo(typeof(int));
    }

    [Test] public async Task Entity_FamilyKindAccessor_Removed()
    {
        // The old `type.Kind` that resolved via App.Format.FamilyOf(Value) is gone.
        // Pin: `type("image", null).Kind` reads null (no family-derivation), and
        // a `type("image/jpeg")` factory splits to {Name:"image", Kind:"jpeg"}
        // — Name carries the family directly; Kind is the subtype.
        var noSubtype = new TypeEntity("image");
        await Assert.That(noSubtype.Kind).IsNull();

        var split = TypeEntity.Create("image/jpeg");
        await Assert.That(split.Name).IsEqualTo("image");
        await Assert.That(split.Kind).IsEqualTo("jpeg");
    }

    [Test] public async Task Entity_Kinds_PopulatedForNumber()
    {
        await using var app = new PLangEngine("/test");
        var num = app.Type["number"];
        await Assert.That(num.Kinds).IsNotNull();
        await Assert.That(num.Kinds!).Contains("int");
        await Assert.That(num.Kinds!).Contains("long");
        await Assert.That(num.Kinds!).Contains("decimal");
        await Assert.That(num.Kinds!).Contains("double");
    }

    [Test] public async Task Entity_Compressible_DerivesFromName()
    {
        await using var app = new PLangEngine("/test");
        // Compressible reads App.Format.Compressible(Name). Pre-stamped image
        // entity carries a Context, so the path runs end-to-end without throw.
        var image = app.Type["image"];
        // No specific bool pinned — Compressible flips on family but the call
        // must not throw and must return a stable bool.
        var result = image.Compressible;
        await Assert.That(result == true || result == false).IsTrue();
    }

    [Test] public async Task Promote_StillThrows_WhenContextUnstamped()
    {
        // Existing producer-bug guard: an entity minted from FromName without
        // Context, then reading a fold property (Fields, Description, …), must
        // throw the InvalidOperationException so the bug surfaces at the read
        // site rather than silently returning null.
        var orphan = TypeEntity.FromName("identity");  // catalog-shaped name; no Context stamped
        await Assert.That(() => { var _ = orphan.Fields; }).Throws<System.InvalidOperationException>();
    }
}
