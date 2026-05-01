using global::App.Actor.Context;
using global::App.Variables;
using global::App.modules.list;

namespace PLang.Tests.App.actions.list;

public class ListSetTests
{
    private (global::App.Actor.Context.@this context, Variables memory) CreateContext()
    {
        var app = new global::App.@this("/app");
        return (app.Context, app.Context.Variables);
    }

    [Test]
    public async Task Set_ValidIndex_UpdatesElement()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Set { Context = context, ListName = new Variable("myList"), Index = 1, Value = new global::App.Data.@this("", "replaced")};
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(list![1]).IsEqualTo("replaced");
    }

    [Test]
    public async Task Set_FirstElement_UpdatesCorrectly()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "old", "keep" });

        var action = new Set { Context = context, ListName = new Variable("myList"), Index = 0, Value = new global::App.Data.@this("", "new")};
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(list![0]).IsEqualTo("new");
        await Assert.That(list[1]).IsEqualTo("keep");
    }

    [Test]
    public async Task Set_OutOfBounds_ReturnsError()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Set { Context = context, ListName = new Variable("myList"), Index = 5, Value = new global::App.Data.@this("", "x")};
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("out of range");
    }

    [Test]
    public async Task Set_NegativeIndex_ReturnsError()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a" });

        var action = new Set { Context = context, ListName = new Variable("myList"), Index = -1, Value = new global::App.Data.@this("", "x")};
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("out of range");
    }

    [Test]
    public async Task Set_NotAList_ReturnsError()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", "not a list");

        var action = new Set { Context = context, ListName = new Variable("myList"), Index = 0, Value = new global::App.Data.@this("", "x")};
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("not a list");
    }

    [Test]
    public async Task Set_NonexistentVariable_ReturnsError()
    {
        var (context, _) = CreateContext();

        var action = new Set { Context = context, ListName = new Variable("missing"), Index = 0, Value = new global::App.Data.@this("", "x")};
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Set_ToNull_Succeeds()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Set { Context = context, ListName = new Variable("myList"), Index = 0, Value = null };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(list![0]).IsNull();
    }
}
