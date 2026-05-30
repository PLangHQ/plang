using app.actor.context;
using app;
using app.variable;
using app.module.list;
using ListResult = global::app.module.list.type.list;

namespace PLang.Tests.App.actions.list;

public class ListTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = new global::app.@this("/app");
        return (app.User.Context, app.User.Context.Variable);
    }

    // --- Add ---

    // list.add stores the WHOLE Data — lists carry Data objects, not raw values,
    // so each element keeps its name/type/context. Readers (global::app.variable.navigator.List,
    // EnumerateItems) unwrap on access; low-level tests look at the Data wrapper.
    private static object? Unwrap(object? slot) =>
        slot is global::app.data.@this d ? d.Value : slot;

    [Test]
    public async Task Add_CreatesNewList()
    {
        var (context, memory) = CreateContext();

        var action = new Add { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "first")};
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(Unwrap(list[0])).IsEqualTo("first");
    }

    [Test]
    public async Task Add_AppendsToExistingList()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Add { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "c")};
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(list!.Count).IsEqualTo(3);
        await Assert.That(Unwrap(list[2])).IsEqualTo("c");
    }

    [Test]
    public async Task Add_InsertsAtIndex()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "c" });

        var action = new Add { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "b"), AtIndex = 1 };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var list = memory.GetValue("myList") as List<object?>;
        await Assert.That(Unwrap(list![1])).IsEqualTo("b");
    }

    // --- Remove ---

    [Test]
    public async Task Remove_ByValue()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Remove { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "b")};
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

        var action = new Remove { Context = context, ListName = new app.variable.@this("myList"), AtIndex = 0 };
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

        var action = new Get { Context = context, ListName = new app.variable.@this("myList"), Index = 1 };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("b");
    }

    [Test]
    public async Task Get_OutOfRange_Fails()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a" });

        var action = new Get { Context = context, ListName = new app.variable.@this("myList"), Index = 5 };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
        await Assert.That(result.Error!.Message).Contains("out of range");
    }

    // --- Count ---

    [Test]
    public async Task Count_ReturnsList()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Count { Context = context, ListName = new app.variable.@this("myList") };
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

        var action = new Contains { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "a")};
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Contains_ReturnsFalse()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Contains { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "z")};
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(false);
    }

    // --- First / Last ---

    [Test]
    public async Task First_ReturnsFirstItem()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "x", "y", "z" });

        var action = new First { Context = context, ListName = new app.variable.@this("myList") };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo("x");
    }

    [Test]
    public async Task Last_ReturnsLastItem()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "x", "y", "z" });

        var action = new Last { Context = context, ListName = new app.variable.@this("myList") };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo("z");
    }

    // --- IndexOf ---

    [Test]
    public async Task IndexOf_FindsItem()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new IndexOf { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "b")};
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(1);
    }

    // --- Sort ---

    [Test]
    public async Task Sort_SortsAscending()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "c", "a", "b" });

        var action = new Sort { Context = context, ListName = new app.variable.@this("myList"), Descending = false };
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

        var action = new Join { Context = context, ListName = new app.variable.@this("myList"), Separator = "-" };
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
        var list = (result.Value as global::app.module.list.type.list)?.value as System.Collections.IList;
        await Assert.That(list!.Count).IsEqualTo(3);
    }

    // --- Reverse ---

    [Test]
    public async Task Reverse_ReversesOrder()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { 1, 2, 3 });

        var action = new Reverse { Context = context, ListName = new app.variable.@this("myList") };
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

        var action = new Unique { Context = context, ListName = new app.variable.@this("myList") };
        var result = await action.Run();

        var list = (result.Value as global::app.module.list.type.list)?.value as List<object?>;
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

        var action = new global::app.module.list.Range { Context = context, Start = 1, End = 5, Step = 1 };
        var result = await action.Run();

        var listResult = result.Value as ListResult;
        await Assert.That(listResult!.count).IsEqualTo(5);
    }

    // --- Any ---

    [Test]
    public async Task Any_MatchFound_ReturnsTrue()
    {
        var (context, memory) = CreateContext();
        memory.Set("items", new List<object?>
        {
            new Dictionary<string, object?> { ["level"] = "low" },
            new Dictionary<string, object?> { ["level"] = "high" }
        });

        var action = new Any
		{
            Context = context,
            ListName = new app.variable.@this("items"),
            Key = "level",
            Operator = new global::app.module.condition.Operator("=="),
            Value = new global::app.data.@this("", "high")
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Any_NoMatch_ReturnsFalse()
    {
        var (context, memory) = CreateContext();
        memory.Set("items", new List<object?>
        {
            new Dictionary<string, object?> { ["level"] = "low" },
            new Dictionary<string, object?> { ["level"] = "medium" }
        });

        var action = new Any
		{
            Context = context,
            ListName = new app.variable.@this("items"),
            Key = "level",
            Operator = new global::app.module.condition.Operator("=="),
            Value = new global::app.data.@this("", "high")
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task Any_EmptyList_ReturnsFalse()
    {
        var (context, memory) = CreateContext();
        memory.Set("items", new List<object?>());

        var action = new Any
		{
            Context = context,
            ListName = new app.variable.@this("items"),
            Key = "level",
            Operator = new global::app.module.condition.Operator("=="),
            Value = new global::app.data.@this("", "high")
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task Any_NotEquals_ReturnsTrue()
    {
        var (context, memory) = CreateContext();
        memory.Set("items", new List<object?>
        {
            new Dictionary<string, object?> { ["status"] = "active" },
            new Dictionary<string, object?> { ["status"] = "inactive" }
        });

        var action = new Any
		{
            Context = context,
            ListName = new app.variable.@this("items"),
            Key = "status",
            Operator = new global::app.module.condition.Operator("!="),
            Value = new global::app.data.@this("", "active")
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    // --- Group ---

    [Test]
    public async Task Group_GroupsByKey()
    {
        var (context, memory) = CreateContext();
        memory.Set("orders", new List<object?>
        {
            new Dictionary<string, object?> { ["customer"] = "Alice", ["total"] = 50 },
            new Dictionary<string, object?> { ["customer"] = "Bob", ["total"] = 30 },
            new Dictionary<string, object?> { ["customer"] = "Alice", ["total"] = 20 }
        });

        var action = new Group { Context = context, ListName = new app.variable.@this("orders"), Key = "customer" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var groups = (result.Value as global::app.module.list.type.list)?.value as List<Dictionary<string, object?>>;
        await Assert.That(groups).IsNotNull();
        await Assert.That(groups!.Count).IsEqualTo(2);

        var alice = groups.First(g => g["key"]?.ToString() == "Alice");
        var aliceItems = alice["steps"] as List<object?>;
        await Assert.That(aliceItems!.Count).IsEqualTo(2);

        var bob = groups.First(g => g["key"]?.ToString() == "Bob");
        var bobItems = bob["steps"] as List<object?>;
        await Assert.That(bobItems!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Group_EmptyList_ReturnsEmpty()
    {
        var (context, memory) = CreateContext();
        memory.Set("items", new List<object?>());

        var action = new Group { Context = context, ListName = new app.variable.@this("items"), Key = "category" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var groups = (result.Value as global::app.module.list.type.list)?.value as List<Dictionary<string, object?>>;
        await Assert.That(groups!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Group_MissingKey_GroupsUnderEmpty()
    {
        var (context, memory) = CreateContext();
        memory.Set("items", new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "Alice" },
            new Dictionary<string, object?> { ["name"] = "Bob" }
        });

        var action = new Group { Context = context, ListName = new app.variable.@this("items"), Key = "category" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var groups = (result.Value as global::app.module.list.type.list)?.value as List<Dictionary<string, object?>>;
        // All items grouped under empty key since "category" doesn't exist
        await Assert.That(groups!.Count).IsEqualTo(1);
        await Assert.That(groups[0]["key"]).IsEqualTo("");
    }

    // --- Flatten ---

    [Test]
    public async Task Flatten_FlattensNestedLists()
    {
        var (context, memory) = CreateContext();
        var nested = new List<object?> { 1, new List<object?> { 2, 3 }, new List<object?> { 4, new List<object?> { 5 } } };
        memory.Set("myList", nested);

        var action = new Flatten { Context = context, ListName = new app.variable.@this("myList") };
        var result = await action.Run();

        var listResult = result.Value as ListResult;
        await Assert.That(listResult!.count).IsEqualTo(5);
    }
}
