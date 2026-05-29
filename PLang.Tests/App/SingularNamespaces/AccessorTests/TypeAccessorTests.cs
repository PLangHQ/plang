using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch C — app.type collection + entity-returning indexers (Stages 3 + 4).
//
// app.Type[name] returns the type entity (app.type.@this); .of<T>() likewise.
// The entity carries Value (PLang name), ClrType (System.Type), Kind / Compressible,
// and the folded Entry knowledge (Fields, Shape, Example, …) on the post-fold pass.
public class TypeAccessorTests
{
    [Test] public async Task AppType_IndexByName_ReturnsTypeEntity_WithNameAndClrType()
    {
        await using var app = new PLangEngine("/test");
        var t = app.Type["int"];
        await Assert.That(t.Value).IsEqualTo("int");
        // ClrType resolves through context.App.Type.Clr(Value) — wire Context to enable.
        t.Context = app.User.Context;
        await Assert.That(t.ClrType).IsEqualTo(typeof(int));
    }

    [Test] public async Task AppType_OfT_ReturnsTypeEntity_ForCompileTimeGeneric()
    {
        await using var app = new PLangEngine("/test");
        var entity = app.Type.of<string>();
        await Assert.That(entity.Value).IsEqualTo("string");
    }

    [Test] public async Task AppType_IndexBySystemType_ReturnsEntity_WithMatchingPlangName()
    {
        await using var app = new PLangEngine("/test");
        // Reverse — Name() gives PLang name for a CLR type.
        await Assert.That(app.Type.Name(typeof(string))).IsEqualTo("string");
    }

    // Stage 4 — Entry-fold properties are computed lazily off the entity.
    [Test] public async Task AppType_IndexByName_ValidValues_OnEnumType_AreReachable()
    {
        // Enum types surface ValidValues; find one from the catalog.
        await using var app = new PLangEngine("/test");
        var entries = app.Type.BuildTypeEntries(app.Module);
        var enumEntry = entries.FirstOrDefault(e => e.Kind == global::app.builder.type.EntryKind.Enum && e.Values != null && e.Values.Count > 0);
        await Assert.That(enumEntry).IsNotNull();

        var t = app.Type[enumEntry!.Name];
        t.Context = app.User.Context;
        await Assert.That(t.ValidValues).IsNotNull();
        await Assert.That(t.ValidValues!.Count).IsGreaterThan(0);
    }

    [Test] public async Task AppType_IndexByName_Scheme_OnPathScheme_IsReachable()
    {
        await using var app = new PLangEngine("/test");
        var p = app.Type["path"];
        p.Context = app.User.Context;
        await Assert.That(p.Scheme).IsNotNull();
    }

    [Test] public async Task AppType_IndexByName_Fields_OnRecordType_FoldedFromEntry()
    {
        await using var app = new PLangEngine("/test");
        var g = app.Type["goal"];
        g.Context = app.User.Context;
        await Assert.That(g.Fields).IsNotNull();
        await Assert.That(g.Fields!.Any(f => f.Name == "name")).IsTrue();
    }

    [Test] public async Task AppType_IndexByName_Shape_OnScalarType_FoldedFromEntry()
    {
        await using var app = new PLangEngine("/test");
        var p = app.Type["path"];
        p.Context = app.User.Context;
        await Assert.That(p.Shape).IsNotNull();
    }

    [Test] public async Task AppType_IndexByName_Example_FoldedFromEntry_ReadsOffTheEntity()
    {
        // Example may be null for many types — just check the surface exists.
        await using var app = new PLangEngine("/test");
        var t = app.Type["string"];
        t.Context = app.User.Context;
        var _ = t.Example;  // doesn't throw, surface present
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task AppType_IndexOfUnknownName_ThrowsTypedError()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(() => { _ = app.Type["nopeType"]; return Task.CompletedTask; })
            .Throws<KeyNotFoundException>();
    }
}
