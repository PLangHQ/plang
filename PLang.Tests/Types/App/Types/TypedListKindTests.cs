namespace PLang.Tests.App.Types;

// A list names its element as its kind ({list, path}); the kind carries the element's class, so the type door
// makes list<path> itself.
public class TypedListKindTests
{
    [Test] public async Task TheTypeDoor_ClosesListOverTheElement()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var typed = app.type.list[new global::app.type.@this("list", "path"), ctx];
        await Assert.That(typed.ClrType).IsEqualTo(typeof(global::app.type.item.list.@this<global::app.type.item.path.@this>));
        await Assert.That(typed.kind.Name).IsEqualTo("path");
    }

    // The face a typed list is read and written by keeps its element — list<goal>, list<path>.
    [Test] public async Task ATypedList_IsWrittenWithItsElement()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        await Assert.That(app.type.list[new global::app.type.@this("list", "goal"), ctx].ToString()).IsEqualTo("list<goal>");
        await Assert.That(app.type.list[new global::app.type.@this("list", "path"), ctx].ToString()).IsEqualTo("list<path>");
        await Assert.That(app.type.list[typeof(global::app.goal.list.@this)]!.ToString()).IsEqualTo("list<goal>");
    }

    [Test] public async Task AnElementKind_IsOneObjectPerElementType()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var a = app.type.list[new global::app.type.@this("list", "text"), ctx].kind;
        var b = app.type.list[new global::app.type.@this("list", "text"), ctx].kind;
        await Assert.That(ReferenceEquals(a, b)).IsTrue();
    }

    [Test] public async Task AValueMadeThroughTheType_IsTheClosedList()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var typed = app.type.list[new global::app.type.@this("list", "path"), ctx];
        var made = await typed.Create(new List<object?> { "a.txt", "b.txt" }, ctx);
        await made.IsSuccess();
        await Assert.That(made.Peek()).IsTypeOf<global::app.type.item.list.@this<global::app.type.item.path.@this>>();
    }

    [Test] public async Task AnUnknownElement_IsNoClosedList()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var typed = app.type.list[new global::app.type.@this("list", "no-such-type"), ctx];
        await Assert.That(typed.ClrType).IsEqualTo(typeof(global::app.type.item.list.@this));
    }
}
