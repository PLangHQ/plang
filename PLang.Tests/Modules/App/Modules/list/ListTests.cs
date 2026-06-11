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
        slot is global::app.data.@this d ? (d.Peek()) : slot;

    [Test]
    public async Task Add_CreatesNewList()
    {
        var (context, memory) = CreateContext();

        var action = new Add { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "first")};
        var result = await action.Run();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.list.@this;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That((await list.At(0)!.Value())?.ToString()).IsEqualTo("first");
    }

    [Test]
    public async Task Add_AppendsToExistingList()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Add { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "c")};
        var result = await action.Run();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.list.@this;
        await Assert.That(list!.Count).IsEqualTo(3);
        await Assert.That((await list.At(2)!.Value())?.ToString()).IsEqualTo("c");
    }

    [Test]
    public async Task Add_InsertsAtIndex()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "c" });

        var action = new Add { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "b"), AtIndex = (global::app.type.number.@this)1 };
        var result = await action.Run();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.list.@this;
        await Assert.That((await list!.At(1)!.Value())?.ToString()).IsEqualTo("b");
    }

    [Test]
    public async Task Add_List_SharesSourceInstance_ReferenceSemantics()
    {
        // Collections are reference semantics: `add %b% to %a%` stores %b%'s
        // list INSTANCE (the entry mints its own Data pointing at it, nothing
        // copied) — in-place mutation of either side shows through both names.
        var (context, memory) = CreateContext();
        var aList = new global::app.type.list.@this { Context = context };
        aList.Add(new global::app.data.@this("", 10L)); aList.Add(new global::app.data.@this("", 20L));
        var bList = new global::app.type.list.@this { Context = context };
        bList.Add(new global::app.data.@this("", 50L)); bList.Add(new global::app.data.@this("", 60L));
        memory.Set("a", aList);
        memory.Set("b", bList);

        var action = new Add { Context = context, ListName = new app.variable.@this("a"), Value = await memory.Get("b") };
        await (await action.Run()).IsSuccess();

        var a = (await memory.GetValue("a")) as global::app.type.list.@this;
        var b = (await memory.GetValue("b")) as global::app.type.list.@this;
        await Assert.That(a!.Count).IsEqualTo(4);   // flat [10,20,50,60]

        // write-through: mutate the leaf in %a% that came from %b% → visible via %b%.
        a.SetAt(2, new global::app.data.@this("", 99L));
        await Assert.That((await a.At(2)!.Value())?.ToString()).IsEqualTo("99");
        await Assert.That((await b!.At(0)!.Value())?.ToString()).IsEqualTo("99");

        // read-view: mutate %b% → %a% flattens through the shared row and tracks it.
        b.Add(new global::app.data.@this("", 70L));
        await Assert.That(a.Count).IsEqualTo(5);
    }

    // --- Remove ---

    [Test]
    public async Task Remove_ByValue()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Remove { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "b")};
        var result = await action.Run();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.list.@this;
        await Assert.That(list!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Remove_ByIndex()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Remove { Context = context, ListName = new app.variable.@this("myList"), AtIndex = (global::app.type.number.@this)0 };
        var result = await action.Run();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.list.@this;
        await Assert.That((await list!.At(0)!.Value())?.ToString()).IsEqualTo("b");
    }

    // --- Get ---

    [Test]
    public async Task Get_ReturnsItemAtIndex()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Get { Context = context, ListName = new app.variable.@this("myList"), Index = (global::app.type.number.@this)1 };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("b");
    }

    [Test]
    public async Task Get_OutOfRange_Fails()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a" });

        var action = new Get { Context = context, ListName = new app.variable.@this("myList"), Index = (global::app.type.number.@this)5 };
        var result = await action.Run();

        await result.IsFailure();
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

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("2");
    }

    // --- Contains ---

    [Test]
    public async Task Contains_ReturnsTrue()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Contains { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "a")};
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
    }

    [Test]
    public async Task Contains_ReturnsFalse()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Contains { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "z")};
        var result = await action.Run();

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
    }

    // --- First / Last ---

    [Test]
    public async Task First_ReturnsFirstItem()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "x", "y", "z" });

        var action = new First { Context = context, ListName = new app.variable.@this("myList") };
        var result = await action.Run();

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("x");
    }

    [Test]
    public async Task Last_ReturnsLastItem()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "x", "y", "z" });

        var action = new Last { Context = context, ListName = new app.variable.@this("myList") };
        var result = await action.Run();

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("z");
    }

    // --- IndexOf ---

    [Test]
    public async Task IndexOf_FindsItem()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new IndexOf { Context = context, ListName = new app.variable.@this("myList"), Value = new global::app.data.@this("", "b")};
        var result = await action.Run();

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("1");
    }

    // --- Sort ---

    [Test]
    public async Task Sort_SortsAscending()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "c", "a", "b" });

        var action = new Sort { Context = context, ListName = new app.variable.@this("myList"), Descending = (global::app.type.@bool.@this)false };
        var result = await action.Run();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.list.@this;
        await Assert.That((await list!.At(0)!.Value())?.ToString()).IsEqualTo("a");
        await Assert.That((await list.At(2)!.Value())?.ToString()).IsEqualTo("c");
    }

    // --- Join ---

    [Test]
    public async Task Join_JoinsWithSeparator()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Join { Context = context, ListName = new app.variable.@this("myList"), Separator = (global::app.type.text.@this)"-" };
        var result = await action.Run();

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("a-b-c");
    }

    // --- Split ---

    [Test]
    public async Task Split_SplitsString()
    {
        var (context, _) = CreateContext();

        var action = new Split { Context = context, Value = (global::app.type.text.@this)"a,b,c", Separator = (global::app.type.text.@this)"," };
        var result = await action.Run();

        await result.IsSuccess();
        var list = ((await result.Value()) as global::app.module.list.type.list)?.value as global::app.type.list.@this;
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

        var list = (await memory.GetValue("myList")) as global::app.type.list.@this;
        await Assert.That((await list!.At(0)!.Value())?.ToString()).IsEqualTo("3");
        await Assert.That((await list.At(2)!.Value())?.ToString()).IsEqualTo("1");
    }

    // --- Unique ---

    [Test]
    public async Task Unique_RemovesDuplicates()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "a", "c", "b" });

        var action = new Unique { Context = context, ListName = new app.variable.@this("myList") };
        var result = await action.Run();

        var list = ((await result.Value()) as global::app.module.list.type.list)?.value as global::app.type.list.@this;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(3);
        var values = list.Items.Select(d => d.Peek()?.ToString()).ToList();
        await Assert.That(values).Contains("a");
        await Assert.That(values).Contains("b");
        await Assert.That(values).Contains("c");
    }

    // --- Range ---

    [Test]
    public async Task Range_GeneratesSequence()
    {
        var (context, _) = CreateContext();

        var action = new global::app.module.list.Range { Context = context, Start = (global::app.type.number.@this)1, End = (global::app.type.number.@this)5, Step = (global::app.type.number.@this)1 };
        var result = await action.Run();

        var listResult = (await result.Value()) as ListResult;
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
            Key = (global::app.type.text.@this)"level",
            Operator = (global::app.type.choice.@this<global::app.module.condition.Operator>)new global::app.module.condition.Operator("=="),
            Value = new global::app.data.@this("", "high")
        };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
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
            Key = (global::app.type.text.@this)"level",
            Operator = (global::app.type.choice.@this<global::app.module.condition.Operator>)new global::app.module.condition.Operator("=="),
            Value = new global::app.data.@this("", "high")
        };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
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
            Key = (global::app.type.text.@this)"level",
            Operator = (global::app.type.choice.@this<global::app.module.condition.Operator>)new global::app.module.condition.Operator("=="),
            Value = new global::app.data.@this("", "high")
        };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
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
            Key = (global::app.type.text.@this)"status",
            Operator = (global::app.type.choice.@this<global::app.module.condition.Operator>)new global::app.module.condition.Operator("!="),
            Value = new global::app.data.@this("", "active")
        };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
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

        var action = new Group { Context = context, ListName = new app.variable.@this("orders"), Key = (global::app.type.text.@this)"customer" };
        var result = await action.Run();

        await result.IsSuccess();
        var groups = ((await result.Value()) as global::app.module.list.type.list)?.value as global::app.type.list.@this;
        await Assert.That(groups).IsNotNull();
        await Assert.That(groups!.Count).IsEqualTo(2);

        await Assert.That(BucketCount(groups, "Alice")).IsEqualTo(2);
        await Assert.That(BucketCount(groups, "Bob")).IsEqualTo(1);
    }

    // Helper: find a group bucket by key and return its items list count.
    private static int BucketCount(global::app.type.list.@this groups, string key)
    {
        foreach (var b in groups.Items)
        {
            var d = (global::app.type.dict.@this)(b.Peek())!;
            if (((d.Get("key")).Peek())?.ToString() == key)
                return (int)((global::app.type.list.@this)((d.Get("items"))!.Peek())!).Count;
        }
        return -1;
    }

    [Test]
    public async Task Group_EmptyList_ReturnsEmpty()
    {
        var (context, memory) = CreateContext();
        memory.Set("items", new List<object?>());

        var action = new Group { Context = context, ListName = new app.variable.@this("items"), Key = (global::app.type.text.@this)"category" };
        var result = await action.Run();

        await result.IsSuccess();
        var groups = ((await result.Value()) as global::app.module.list.type.list)?.value as global::app.type.list.@this;
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

        var action = new Group { Context = context, ListName = new app.variable.@this("items"), Key = (global::app.type.text.@this)"category" };
        var result = await action.Run();

        await result.IsSuccess();
        var groups = ((await result.Value()) as global::app.module.list.type.list)?.value as global::app.type.list.@this;
        // All items grouped under empty key since "category" doesn't exist
        await Assert.That(groups!.Count).IsEqualTo(1);
        await Assert.That((await ((global::app.type.dict.@this)(await groups.At(0)!.Value())!).Get("key")!.Value())?.ToString()).IsEqualTo("");
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

        var listResult = (await result.Value()) as ListResult;
        await Assert.That(listResult!.count).IsEqualTo(5);
    }
}
