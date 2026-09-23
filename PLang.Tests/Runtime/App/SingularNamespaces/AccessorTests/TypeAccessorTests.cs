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

    [Test] public async Task AppType_IndexByRuntimeType_ReturnsTypeEntity()
    {
        await using var app = TestApp.Create("/test");
        var entity = app.Type[typeof(string)];
        await Assert.That(entity.Name).IsEqualTo("text");
    }

    [Test] public async Task AppType_IndexBySystemType_ReturnsEntity_WithMatchingPlangName()
    {
        await using var app = TestApp.Create("/test");
        // Reverse — Name() gives PLang name for a CLR type.
        await Assert.That(app.Type[typeof(string)].ToString()).IsEqualTo("text");
    }

    // A choice is {choice, kind: <its set>}, and its entity carries the set's options.
    [Test] public async Task AppType_IndexByClr_Choice_IsChoiceWithSetKindAndValues()
    {
        await using var app = TestApp.Create("/test");
        var t = app.Type[typeof(global::app.type.item.choice.@this<global::app.module.action.condition.Operator>)];
        await Assert.That(t.Name).IsEqualTo("choice");
        await Assert.That(t.Kind?.Name).IsEqualTo("operator");
        await Assert.That(t.Values!).Contains("==");
    }

    // A closed set's name is a kind, never a type of its own.
    [Test] public async Task AppType_SetName_IsNotATypeName()
    {
        await using var app = TestApp.Create("/test");
        await Assert.That(app.Type.Contains("operator")).IsFalse();
        await Assert.That(app.Type.Contains("choice")).IsTrue();
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
