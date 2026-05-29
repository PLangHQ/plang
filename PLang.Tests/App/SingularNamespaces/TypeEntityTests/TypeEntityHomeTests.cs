using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.TypeEntityTests;

// Batch F — Stage 4: the type entity at its new home and with folded Entry knowledge.
// plang-types already shipped data.Type as an entity (app.type.@this). Stage 4 *moves* it to type.@this,
// *demotes* the registry to type.list.@this, and *folds* builder.Types.Entry onto the entity.
//
// THIS STAGE WAS NOT EXECUTED IN coder v1 — see report.md. Tests stay at Assert.Fail
// with explicit Stage 4 deferral notes.
public class TypeEntityHomeTests
{
    [Test] public async Task DataType_ClrType_ReturnsSystemType_RegressionFromPlangTypes()
    {
        // plang-types ALREADY shipped this — the regression pin still passes today
        // even though the entity lives at app.type.@this (Stage 4 moves it).
        await using var app = new PLangEngine("/test");
        var d = new global::app.data.@this<int>("", 42) { Context = app.User.Context };
        await Assert.That(d.Type).IsNotNull();
        await Assert.That(d.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test] public async Task DataType_OnStampedData_ResolvesViaAppTypeIndexer()
    {
        // ClrType walks through context.app.Types.Clr today — same result as app.Type[Value].
        await using var app = new PLangEngine("/test");
        var d = new global::app.data.@this<string>("", "hello") { Context = app.User.Context };
        await Assert.That(d.Type!.ClrType).IsEqualTo(app.Type[d.Type.Value]);
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
        await using var app = new PLangEngine("/test");
        var d = new global::app.data.@this<int>("", 42) { Context = app.User.Context };
        var typeFromData = d.Type;
        var clrFromRegistry = app.Type["int"];
        await Assert.That(typeFromData).IsNotNull();
        await Assert.That(typeFromData!.GetType()).IsEqualTo(typeof(global::app.type.@this));
        await Assert.That(typeFromData.ClrType).IsEqualTo(clrFromRegistry);
    }

    [Test] public async Task TypeEntity_OnRecordType_FoldedEntryFields_AreReadableOffTheEntity()
        => Assert.Fail("Stage 4 deferral — Entry fold");

    [Test] public async Task BuilderTypesEntry_FieldAndEntryKind_TypesDoNotExist_AfterFold()
        => Assert.Fail("Stage 4 deferral — Entry fold dissolves these types");

    [Test] public async Task DataConverter_NewtonsoftTypeConverter_DoesNotExist_AfterMove()
        => Assert.Fail("Stage 4 deferral — Converter.cs deleted with entity move");
}
