using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.TypeEntityTests;

// Batch F — Stage 4: the type entity at its new home and with folded Entry knowledge.
// plang-types already shipped data.Type as an entity (app.type.@this). Stage 4 *moves* it to type.@this,
// *demotes* the registry to type.catalog.@this, and *folds* builder.Types.Entry onto the entity.
//
// THIS STAGE WAS NOT EXECUTED IN coder v1 — see report.md. Tests stay at Assert.Fail
// with explicit Stage 4 deferral notes.
public class TypeEntityHomeTests
{
    [Test] public async Task DataType_ClrType_ReturnsSystemType_RegressionFromPlangTypes()
    {
        // plang-types ALREADY shipped this — the regression pin still passes today
        // even though the entity lives at app.type.@this (Stage 4 moves it).
        await using var app = TestApp.Create("/test");
        var d = new global::app.data.@this<global::app.type.item.number.@this>("", 42, context: app.User.Context);
        await Assert.That(d.Type).IsNotNull();
        await Assert.That(d.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test] public async Task DataType_OnStampedData_ResolvesViaAppTypeIndexer()
    {
        // Every scalar is an item now (text/number/datetime/.../guid all have a
        // `: item` wrapper), so the value door's ClrType is the wrapper class while
        // the registry door's ClrType is the bare CLR type — a legitimate
        // name/@this duality. The stable identity a stamped Data resolves by is the
        // NAME, so that is what the app.Type indexer round-trips on.
        await using var app = TestApp.Create("/test");
        var d = new global::app.data.@this("", System.Guid.NewGuid(), context: app.User.Context);
        var fromRegistry = app.Type[d.Type!.Name];
        fromRegistry.Context = app.User.Context;
        await Assert.That(d.Type.Name).IsEqualTo(fromRegistry.Name);
    }

    [Test] public async Task TypeEntity_LivesAt_TypeNamespace_NotAppDataNamespace()
    {
        // Stage 4 (minimal): the entity moved to app.type.@this.
        // (Reflection name is "app.type.this" — @ is a source-level keyword escape, stripped in metadata.)
        var asm = typeof(global::app.@this).Assembly;
        await Assert.That(typeof(global::app.type.@this)).IsNotNull();
        await Assert.That(asm.GetType("app.data.type")).IsNull();
    }

    [Test] public async Task DataType_And_AppTypeByName_ReturnEquivalentEntity_ForSamePlangName()
    {
        // Both doors return values of the SAME entity type (app.type.@this).
        await using var app = TestApp.Create("/test");
        var d = new global::app.data.@this<global::app.type.item.number.@this>("", 42, context: app.User.Context);
        var typeFromData = d.Type;
        var entityFromRegistry = app.Type["int"];
        entityFromRegistry.Context = app.User.Context;
        await Assert.That(typeFromData).IsNotNull();
        await Assert.That(typeFromData!.GetType()).IsEqualTo(typeof(global::app.type.@this));
        await Assert.That(typeFromData.ClrType).IsEqualTo(entityFromRegistry.ClrType);
    }

    [Test] public async Task TypeEntity_OnRecordType_FoldedEntryFields_AreReadableOffTheEntity()
    {
        await using var app = TestApp.Create("/test");
        var entries = app.Type.BuildTypeEntries(app.Module);
        var record = entries.FirstOrDefault(e => e.Fields != null && e.Fields.Count > 0);
        await Assert.That(record).IsNotNull();
        await Assert.That(record!.Fields).IsNotNull();
        await Assert.That(record.Fields!.Count).IsGreaterThan(0);
    }

    [Test] public async Task BuilderTypesEntry_FieldAndEntryKind_TypesDoNotExist_AfterFold()
    {
        var asm = typeof(global::app.@this).Assembly;
        await Assert.That(asm.GetType("app.builder.type.Entry")).IsNull();
        await Assert.That(asm.GetType("app.builder.type.Field")).IsNull();
        await Assert.That(asm.GetType("app.builder.type.EntryKind")).IsNull();
    }

    [Test] public async Task DataConverter_NewtonsoftTypeConverter_DoesNotExist_AfterMove()
    {
        var asm = typeof(global::app.@this).Assembly;
        await Assert.That(asm.GetType("app.data.Converter")).IsNull();
    }
}
