using app.actor.context;
using app.variable;
using app.module.action.list;

namespace PLang.Tests.App.actions.list;

public class ListSetTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = TestApp.Create("/app");
        return (app.User.Context, app.User.Context.Variable);
    }

    [Test]
    public async Task Set_ValidIndex_UpdatesElement()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Set(context) { ListName = new app.variable.@this("myList"), Index = (global::app.type.item.number.@this)1, Value = new global::app.data.@this("", "replaced", context: context)};
        var result = await action.Run();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.item.list.@this;
        await Assert.That((await list!.At(1)!.Value())?.ToString()).IsEqualTo("replaced");
    }

    [Test]
    public async Task Set_FirstElement_UpdatesCorrectly()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "old", "keep" });

        var action = new Set(context) { ListName = new app.variable.@this("myList"), Index = (global::app.type.item.number.@this)0, Value = new global::app.data.@this("", "new", context: context)};
        var result = await action.Run();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.item.list.@this;
        await Assert.That((await list!.At(0)!.Value())?.ToString()).IsEqualTo("new");
        await Assert.That((await list.At(1)!.Value())?.ToString()).IsEqualTo("keep");
    }

    [Test]
    public async Task Set_OutOfBounds_ReturnsError()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Set(context) { ListName = new app.variable.@this("myList"), Index = (global::app.type.item.number.@this)5, Value = new global::app.data.@this("", "x", context: context)};
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("out of range");
    }

    [Test]
    public async Task Set_NegativeIndex_ReturnsError()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a" });

        var action = new Set(context) { ListName = new app.variable.@this("myList"), Index = (global::app.type.item.number.@this)(-1), Value = new global::app.data.@this("", "x", context: context)};
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("out of range");
    }

    [Test]
    public async Task Set_NotAList_ReturnsError()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", "not a list");

        var action = new Set(context) { ListName = new app.variable.@this("myList"), Index = (global::app.type.item.number.@this)0, Value = new global::app.data.@this("", "x", context: context)};
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("not a list");
    }

    [Test]
    public async Task Set_NonexistentVariable_ReturnsError()
    {
        var (context, _) = CreateContext();

        var action = new Set(context) { ListName = new app.variable.@this("missing"), Index = (global::app.type.item.number.@this)0, Value = new global::app.data.@this("", "x", context: context)};
        var result = await action.Run();

        await result.IsFailure();
    }

    [Test]
    public async Task Set_ToNull_Succeeds()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Set(context) { ListName = new app.variable.@this("myList"), Index = (global::app.type.item.number.@this)0, Value = null };
        var result = await action.Run();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.item.list.@this;
        await Assert.That(await (await list!.At(0)!.Value())!.IsEmpty()).IsTrue();
    }
}
