using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// Public surface of `app.type.@this`. Name is the family (e.g. "image"); Kind is
// the subtype (e.g. "gif"); Strict is a bool; ClrType is non-public (interior callers
// reach it via the registry App.Type.Get/.Clr).

public class TypeEntityShapeTests
{
    [Test] public async Task Entity_HasName_NotValue()
    {
        // Reflection probe: app.type.@this has a public `Name` property; `Value` is gone.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Entity_HasKindAndStrict_AsTopLevelMembers()
    {
        // Public `string? Kind` and `bool Strict` directly on the entity.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Entity_ClrType_NotOnPublicSurface()
    {
        // Reflection: no public `ClrType` property on app.type.@this.
        // Interior callers reach it via App.Type.Get/.Clr (the registry).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Entity_FamilyKindAccessor_Removed()
    {
        // The old `type.Kind` that resolved via App.Format.KindOf(Value) is gone.
        // Family IS the Name now — type("image/jpeg") collapses to Name "image"
        // (or Name "image", Kind "jpeg"; see TypeFactoryTests slash split).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Entity_Kinds_PopulatedForNumber()
    {
        // The advertised-vocabulary `Kinds` list (distinct from per-value Kind) survives
        // for `number` — ["int","long","decimal","double"]. The naming knot is resolved:
        // Name=family, Kind=subtype, Kinds=advertised vocabulary.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Entity_Compressible_DerivesFromName()
    {
        // Compressible used to read App.Format.KindOf-style family. Now it
        // derives from Name (the family). image is compressible? — coder picks the
        // exact set; pin that it doesn't break.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Promote_StillThrows_WhenContextUnstamped()
    {
        // Existing producer-bug guard (the InvalidOperationException when an unstamped
        // type.@this reads a catalog prop) must survive the Name/Kind/Strict additions.
        // _foldLoaded discipline unchanged.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
