using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch C — app.type collection + entity-returning indexers (Stages 3 + 4).
//
// app.type.list[name] returns the catalog-built entity (app.type.@this); .of<T>() likewise.
// The entity carries Value (PLang name), ClrType (System.Type) pre-stamped from the
// registry, and the folded Entry knowledge (Fields, Shape, Example, …) — all populated
// at construction, no manual Context stamp needed.
public class TypeAccessorTests
{
    [Test] public async Task AppType_IndexByName_ReturnsTypeEntity_WithNameAndClrType()
    {
        await using var app = new global::app.@this("/test").Testing();
        var t = app.type.list[new global::app.type.@this("number", "int"), app.actor.list.User.Context];
        await Assert.That(t.Name).IsEqualTo("number");
        await Assert.That(t.ClrType).IsEqualTo(typeof(global::app.type.item.number.@this));
    }

    [Test] public async Task AppType_IndexByRuntimeType_ReturnsTypeEntity()
    {
        await using var app = new global::app.@this("/test").Testing();
        var entity = app.type.list[typeof(string)];
        await Assert.That(entity.Name).IsEqualTo("text");
    }

    [Test] public async Task AppType_IndexBySystemType_ReturnsEntity_WithMatchingPlangName()
    {
        await using var app = new global::app.@this("/test").Testing();
        // Reverse — Name() gives PLang name for a CLR type.
        await Assert.That(app.type.list[typeof(string)].ToString()).IsEqualTo("text");
    }

    // A choice is {choice, kind: <its set>}, and its entity carries the set's options.
    [Test] public async Task AppType_IndexByClr_Choice_IsChoiceWithSetKindAndValues()
    {
        await using var app = new global::app.@this("/test").Testing();
        var t = app.type.list[typeof(global::app.type.item.choice.@this<global::app.data.Operator>)];
        await Assert.That(t.Name).IsEqualTo("choice");
        await Assert.That(t.kind.Name).IsEqualTo("operator");
        await Assert.That(t.Values!).Contains("==");
    }

    // A closed set's name is a kind, never a type of its own.
    [Test] public async Task AppType_SetName_IsNotATypeName()
    {
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That(app.type.list.Contains("operator")).IsFalse();
        await Assert.That(app.type.list.Contains("choice")).IsTrue();
    }

    [Test] public async Task AppType_IndexByName_Fields_OnRecordType_FoldedFromEntry()
    {
        await using var app = new global::app.@this("/test").Testing();
        var g = app.type.list["goal"];
        await Assert.That(g.Property).IsNotNull();
        await Assert.That(g.Property!.Any(f => f.Name == "name")).IsTrue();
    }

    [Test] public async Task AppType_IndexByName_Shape_OnScalarType_FoldedFromEntry()
    {
        await using var app = new global::app.@this("/test").Testing();
        var p = app.type.list["path"];
        await Assert.That(p.Shape).IsNotNull();
    }

    // what the builder shows the model for a permission: its {path, verbs} form
    [Test] public async Task APermission_ShowsItsPathAndVerbsForm()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var p = app.type.list["permission"];
        await Assert.That(await p.Example(ctx).Text(ctx)).IsEqualTo("{\"path\": \"/src/os\", \"verbs\": [\"read\", \"write\"]}");
        await Assert.That(p.Shape).IsEqualTo("object");
        await Assert.That(await p.Description(ctx).Text(ctx)).IsNotEmpty();
    }

    // a list of records shows one of its element, and names it — so a list<permission> slot teaches {path, verbs}
    [Test] public async Task AListOfPermissions_ShowsOneOfItsElement()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var listed = app.type.list[new global::app.type.@this("list", "permission"), ctx];
        await Assert.That(await listed.Example(ctx).Text(ctx)).IsEqualTo("[{\"path\": \"/src/os\", \"verbs\": [\"read\", \"write\"]}]");
        await Assert.That(await listed.Description(ctx).Text(ctx)).StartsWith("A list of permission: What may be done where");
    }

    // a record is written as an object: one with builder properties, or one that declares the object shape
    [Test] public async Task ARecord_IsWrittenAsAnObject_AScalarIsNot()
    {
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That(app.type.list["permission"].IsRecord).IsTrue();
        await Assert.That(app.type.list["goal"].IsRecord).IsTrue();
        await Assert.That(app.type.list["text"].IsRecord).IsFalse();
        await Assert.That(app.type.list["path"].IsRecord).IsFalse();
    }

    // a list of any other element, and a list with none, says the list's own
    [Test] public async Task AListOfTextsOrOfAnything_ShowsTheListsOwn()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var plain = app.type.list["list"];
        var texts = app.type.list[new global::app.type.@this("list", "text"), ctx];
        await Assert.That(await texts.Example(ctx).Text(ctx)).IsEqualTo(await plain.Example(ctx).Text(ctx));
        await Assert.That(await texts.Description(ctx).Text(ctx)).IsEqualTo(await plain.Description(ctx).Text(ctx));
    }

    // a type's example is its teaching file's — text shows `Hello, world`, a type with no file reads empty, never throws
    [Test] public async Task AppType_Example_IsItsTeachingFile_AndAbsentReadsEmpty()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await Assert.That(await app.type.list["string"].Example(ctx).Text(ctx)).IsEqualTo("Hello, world");
        await Assert.That(await app.type.list["type"].Example(ctx).Text(ctx)).IsEqualTo("");
        await Assert.That(await app.type.list["type"].Description(ctx).Text(ctx)).IsEqualTo("");
        // read as a template reads it: a type that says nothing answers no value, so `{% if t.Description %}` is false
        var read = await (await new global::app.data.@this("", app.type.list["type"], context: ctx).Get("Description")).Value();
        await Assert.That(read is null || read.IsNull).IsTrue();
    }

    [Test] public async Task AppType_IndexOfUnknownName_ThrowsTypedError()
    {
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That(() => { _ = app.type.list["nopeType"]; return Task.CompletedTask; })
            .Throws<KeyNotFoundException>();
    }
}
