using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.list;

namespace PLang.Tests.Runtime2.actions.list;

public class ListSetTests
{
    private (PLangContext context, MemoryStack memory) CreateContext()
    {
        var engine = new PLang.Runtime2.Engine.@this("/app");
        var memory = new MemoryStack();
        var context = new PLangContext(engine, memory);
        return (context, memory);
    }

    [Test]
    public async Task Set_ValidIndex_UpdatesElement()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Set { Context = context, ListName = "myList", Index = 1, Value = "replaced" };
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

        var action = new Set { Context = context, ListName = "myList", Index = 0, Value = "new" };
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

        var action = new Set { Context = context, ListName = "myList", Index = 5, Value = "x" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("out of range");
    }

    [Test]
    public async Task Set_NegativeIndex_ReturnsError()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a" });

        var action = new Set { Context = context, ListName = "myList", Index = -1, Value = "x" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("out of range");
    }

    [Test]
    public async Task Set_NotAList_ReturnsError()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", "not a list");

        var action = new Set { Context = context, ListName = "myList", Index = 0, Value = "x" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("not a list");
    }

    [Test]
    public async Task Set_NonexistentVariable_ReturnsError()
    {
        var (context, _) = CreateContext();

        var action = new Set { Context = context, ListName = "missing", Index = 0, Value = "x" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Set_ToNull_Succeeds()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Set { Context = context, ListName = "myList", Index = 0, Value = null };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(list![0]).IsNull();
    }
}
