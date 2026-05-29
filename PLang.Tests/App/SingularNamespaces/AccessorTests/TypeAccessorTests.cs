using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch C — app.type collection + entity-returning indexers (Stages 3 + 4).
//
// In this Stage 3 (minimal) pass `app.Type[name]` returns the raw System.Type — the move
// to a type.@this entity is Stage 4's job.  These tests pin the indexer/of<T>() surface
// today and the entity-shape tests (Fields/Shape/Example/Scheme/ValidValues on the entity)
// stay at Assert.Fail("Not implemented") until Stage 4 lands.
public class TypeAccessorTests
{
    [Test] public async Task AppType_IndexByName_ReturnsTypeEntity_WithNameAndClrType()
    {
        await using var app = new PLangEngine("/test");
        var t = app.Type["int"];
        await Assert.That(t).IsEqualTo(typeof(int));
    }

    [Test] public async Task AppType_OfT_ReturnsTypeEntity_ForCompileTimeGeneric()
    {
        await using var app = new PLangEngine("/test");
        var name = app.Type.of<string>();
        await Assert.That(name).IsEqualTo("string");
    }

    [Test] public async Task AppType_IndexBySystemType_ReturnsEntity_WithMatchingPlangName()
    {
        await using var app = new PLangEngine("/test");
        // Reverse — Name() gives PLang name for a CLR type.
        await Assert.That(app.Type.Name(typeof(string))).IsEqualTo("string");
    }

    // Stage 4 deliverables — gated.
    [Test] public async Task AppType_IndexByName_ValidValues_OnEnumType_AreReachable()
        => Assert.Fail("Stage 4 — type entity move folds Entry");

    [Test] public async Task AppType_IndexByName_Scheme_OnPathScheme_IsReachable()
        => Assert.Fail("Stage 4 — type entity move folds Entry");

    [Test] public async Task AppType_IndexByName_Fields_OnRecordType_FoldedFromEntry()
        => Assert.Fail("Stage 4 — Entry fold");

    [Test] public async Task AppType_IndexByName_Shape_OnScalarType_FoldedFromEntry()
        => Assert.Fail("Stage 4 — Entry fold");

    [Test] public async Task AppType_IndexByName_Example_FoldedFromEntry_ReadsOffTheEntity()
        => Assert.Fail("Stage 4 — Entry fold");

    [Test] public async Task AppType_IndexOfUnknownName_ThrowsTypedError()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(() => { _ = app.Type["nopeType"]; return Task.CompletedTask; })
            .Throws<KeyNotFoundException>();
    }
}
