using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.actions.list;
using ListResult = PLang.Runtime2.actions.list.types.list;

namespace PLang.Tests.Runtime2.actions.list;

public class ListTests
{
    private (PLangContext context, MemoryStack memory) CreateContext()
    {
        var engine = new PLang.Runtime2.Engine.@this("/app");
        var memory = new MemoryStack();
        var context = new PLangContext(engine, memory);
        return (context, memory);
    }

    // --- Add ---

    [Test]
    public async Task Add_CreatesNewList()
    {
        var (context, memory) = CreateContext();

        var action = new Add { Context = context, ListName = "myList", Value = "first" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0]).IsEqualTo("first");
    }

    [Test]
    public async Task Add_AppendsToExistingList()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Add { Context = context, ListName = "myList", Value = "c" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(list!.Count).IsEqualTo(3);
        await Assert.That(list[2]).IsEqualTo("c");
    }

    [Test]
    public async Task Add_InsertsAtIndex()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "c" });

        var action = new Add { Context = context, ListName = "myList", Value = "b", AtIndex = 1 };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(list![1]).IsEqualTo("b");
    }

    // --- Remove ---

    [Test]
    public async Task Remove_ByValue()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Remove { Context = context, ListName = "myList", Value = "b" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(list!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Remove_ByIndex()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Remove { Context = context, ListName = "myList", AtIndex = 0 };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(list![0]).IsEqualTo("b");
    }

    // --- Get ---

    [Test]
    public async Task Get_ReturnsItemAtIndex()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Get { Context = context, ListName = "myList", Index = 1 };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("b");
    }

    [Test]
    public async Task Get_OutOfRange_Fails()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a" });

        var action = new Get { Context = context, ListName = "myList", Index = 5 };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    // --- Count ---

    [Test]
    public async Task Count_ReturnsList()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Count { Context = context, ListName = "myList" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(2);
    }

    // --- Contains ---

    [Test]
    public async Task Contains_ReturnsTrue()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Contains { Context = context, ListName = "myList", Value = "a" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Contains_ReturnsFalse()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Contains { Context = context, ListName = "myList", Value = "z" };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(false);
    }

    // --- First / Last ---

    [Test]
    public async Task First_ReturnsFirstItem()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "x", "y", "z" });

        var action = new First { Context = context, ListName = "myList" };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo("x");
    }

    [Test]
    public async Task Last_ReturnsLastItem()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "x", "y", "z" });

        var action = new Last { Context = context, ListName = "myList" };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo("z");
    }

    // --- IndexOf ---

    [Test]
    public async Task IndexOf_FindsItem()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new IndexOf { Context = context, ListName = "myList", Value = "b" };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(1);
    }

    // --- Sort ---

    [Test]
    public async Task Sort_SortsAscending()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "c", "a", "b" });

        var action = new Sort { Context = context, ListName = "myList", Descending = false };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(list![0]).IsEqualTo("a");
        await Assert.That(list[2]).IsEqualTo("c");
    }

    // --- Join ---

    [Test]
    public async Task Join_JoinsWithSeparator()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Join { Context = context, ListName = "myList", Separator = "-" };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo("a-b-c");
    }

    // --- Split ---

    [Test]
    public async Task Split_SplitsString()
    {
        var (context, _) = CreateContext();

        var action = new Split { Context = context, Value = "a,b,c", Separator = "," };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = result.Value as System.Collections.IList;
        await Assert.That(list!.Count).IsEqualTo(3);
    }

    // --- Reverse ---

    [Test]
    public async Task Reverse_ReversesOrder()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { 1, 2, 3 });

        var action = new Reverse { Context = context, ListName = "myList" };
        var result = await action.Run();

        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(list![0]).IsEqualTo(3);
        await Assert.That(list[2]).IsEqualTo(1);
    }

    // --- Unique ---

    [Test]
    public async Task Unique_RemovesDuplicates()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "a", "c", "b" });

        var action = new Unique { Context = context, ListName = "myList" };
        var result = await action.Run();

        var list = result.Value as List<object?>;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(3);
        await Assert.That(list).Contains("a");
        await Assert.That(list).Contains("b");
        await Assert.That(list).Contains("c");
    }

    // --- Range ---

    [Test]
    public async Task Range_GeneratesSequence()
    {
        var (context, _) = CreateContext();

        var action = new PLang.Runtime2.actions.list.Range { Context = context, Start = 1, End = 5, Step = 1 };
        var result = await action.Run();

        var listResult = result.Value as ListResult;
        await Assert.That(listResult!.count).IsEqualTo(5);
    }

    // --- Flatten ---

    [Test]
    public async Task Flatten_FlattensNestedLists()
    {
        var (context, memory) = CreateContext();
        var nested = new List<object?> { 1, new List<object?> { 2, 3 }, new List<object?> { 4, new List<object?> { 5 } } };
        memory.Set("myList", nested);

        var action = new Flatten { Context = context, ListName = "myList" };
        var result = await action.Run();

        var listResult = result.Value as ListResult;
        await Assert.That(listResult!.count).IsEqualTo(5);
    }
}
