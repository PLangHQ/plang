using app.actor.context;
using app.type.item.variable;
using app.module.list;

namespace PLang.Tests.App.actions.list;

public class ListSetTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = new global::app.@this("/app").Testing();
        return (app.actor.list.User.Context, app.actor.list.User.Context.Variable);
    }

    [Test]
    public async Task Set_ValidIndex_UpdatesElement()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Set(context) { ListName = new app.type.item.variable.@this("myList"), Index = (global::app.type.item.number.@this)1, Value = new global::app.data.@this("", "replaced", context: context)};
        var result = await action.Start();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.item.list.@this;
        await Assert.That((await list!.At(1, app.actor.list.User.Context)!.Value())?.ToString()).IsEqualTo("replaced");
    }

    [Test]
    public async Task Set_FirstElement_UpdatesCorrectly()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "old", "keep" });

        var action = new Set(context) { ListName = new app.type.item.variable.@this("myList"), Index = (global::app.type.item.number.@this)0, Value = new global::app.data.@this("", "new", context: context)};
        var result = await action.Start();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.item.list.@this;
        await Assert.That((await list!.At(0, app.actor.list.User.Context)!.Value())?.ToString()).IsEqualTo("new");
        await Assert.That((await list.At(1, app.actor.list.User.Context)!.Value())?.ToString()).IsEqualTo("keep");
    }

    [Test]
    public async Task Set_OutOfBounds_ReturnsError()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Set(context) { ListName = new app.type.item.variable.@this("myList"), Index = (global::app.type.item.number.@this)5, Value = new global::app.data.@this("", "x", context: context)};
        var result = await action.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("out of range");
    }

    [Test]
    public async Task Set_NegativeIndex_ReturnsError()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a" });

        var action = new Set(context) { ListName = new app.type.item.variable.@this("myList"), Index = (global::app.type.item.number.@this)(-1), Value = new global::app.data.@this("", "x", context: context)};
        var result = await action.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("out of range");
    }

    [Test]
    public async Task Set_NotAList_ReturnsError()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", "not a list");

        var action = new Set(context) { ListName = new app.type.item.variable.@this("myList"), Index = (global::app.type.item.number.@this)0, Value = new global::app.data.@this("", "x", context: context)};
        var result = await action.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("not a list");
    }

    [Test]
    public async Task Set_NonexistentVariable_ReturnsError()
    {
        var (context, _) = CreateContext();

        var action = new Set(context) { ListName = new app.type.item.variable.@this("missing"), Index = (global::app.type.item.number.@this)0, Value = new global::app.data.@this("", "x", context: context)};
        var result = await action.Start();

        await result.IsFailure();
    }

    [Test]
    public async Task Set_ToNull_Succeeds()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Set(context) { ListName = new app.type.item.variable.@this("myList"), Index = (global::app.type.item.number.@this)0, Value = null };
        var result = await action.Start();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.item.list.@this;
        await Assert.That((await list!.At(0, app.actor.list.User.Context)!.Value())!.IsTruthy()).IsFalse();
    }
}
