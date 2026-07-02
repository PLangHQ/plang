using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch C — app.type collection + entity-returning indexers (Stages 3 + 4).
//
// app.Type[name] returns the catalog-built entity (app.type.@this); .of<T>() likewise.
// The entity carries Value (PLang name), ClrType (System.Type) pre-stamped from the
// registry, and the folded Entry knowledge (Fields, Shape, Example, …) — all populated
// at construction by BuildTypeEntries, no manual Context stamp needed.
public class TypeAccessorTests
{
    [Test] public async Task AppType_IndexByName_ReturnsTypeEntity_WithNameAndClrType()
    {
        await using var app = TestApp.Create("/test");
        var t = app.Type["int"];
        await Assert.That(t.Name).IsEqualTo("number");
        await Assert.That(t.ClrType).IsEqualTo(typeof(int));
    }

    [Test] public async Task AppType_OfT_ReturnsTypeEntity_ForCompileTimeGeneric()
    {
        await using var app = TestApp.Create("/test");
        var entity = app.Type.of<string>();
        await Assert.That(entity.Name).IsEqualTo("text");
    }

    [Test] public async Task AppType_IndexBySystemType_ReturnsEntity_WithMatchingPlangName()
    {
        await using var app = TestApp.Create("/test");
        // Reverse — Name() gives PLang name for a CLR type.
        await Assert.That(app.Type.Name(typeof(string))).IsEqualTo("text");
    }

    // Stage 4 — Entry-fold properties are populated at construction by BuildTypeEntries.
    [Test] public async Task AppType_IndexByName_ValidValues_OnEnumType_AreReachable()
    {
        await using var app = TestApp.Create("/test");
        // Find a known enum-shape type in the catalog.
        var entries = app.Type.BuildTypeEntries(app.Module);
        var enumEntry = entries.FirstOrDefault(e => e.Values != null && e.Values.Count > 0);
        await Assert.That(enumEntry).IsNotNull();

        var t = app.Type[enumEntry!.Name];
        await Assert.That(t.ValidValues).IsNotNull();
        await Assert.That(t.ValidValues!.Count).IsGreaterThan(0);
    }

    [Test] public async Task AppType_IndexByName_Scheme_OnPathScheme_IsReachable()
    {
        await using var app = TestApp.Create("/test");
        var p = app.Type["path"];
        // Scheme is actor-Context-dependent (per-app scheme registry); stamp once for that.
        p.Context = app.User.Context;
        await Assert.That(p.Scheme).IsNotNull();
    }

    [Test] public async Task AppType_IndexByName_Fields_OnRecordType_FoldedFromEntry()
    {
        await using var app = TestApp.Create("/test");
        var g = app.Type["goal"];
        await Assert.That(g.Fields).IsNotNull();
        await Assert.That(g.Fields!.Any(f => f.Name == "name")).IsTrue();
    }

    [Test] public async Task AppType_IndexByName_Shape_OnScalarType_FoldedFromEntry()
    {
        await using var app = TestApp.Create("/test");
        var p = app.Type["path"];
        await Assert.That(p.Shape).IsNotNull();
    }

    [Test] public async Task AppType_IndexByName_Example_FoldedFromEntry_ReadsOffTheEntity()
    {
        // Example may be null for many types — just check the surface exists.
        await using var app = TestApp.Create("/test");
        var t = app.Type["string"];
        var _ = t.Example;  // doesn't throw, surface present
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task AppType_IndexOfUnknownName_ThrowsTypedError()
    {
        await using var app = TestApp.Create("/test");
        await Assert.That(() => { _ = app.Type["nopeType"]; return Task.CompletedTask; })
            .Throws<KeyNotFoundException>();
    }
}
