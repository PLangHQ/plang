using app.actor.context;
using app;

namespace PLang.Tests.App.actions.list;

using ListResult = global::app.module.list.type.list;

/// <summary>
/// list-module behavior through the real path — Make.Goal -> RealGoalLoad.ViaChannel
/// -> RunGoalAsync, asserting on observable state (returned Data / stored list / error).
/// Replaces the hand-constructed `new Add{...}.Run()` unit style. Reference-identity
/// contracts (ReferenceEquals on the live list) have no language surface and stay in
/// ListAddIdentityTests (floor).
/// </summary>
public class ListGoalRunTests
{
    static async Task<(global::app.@this engine, global::app.actor.context.@this ctx, global::app.data.@this result)>
        Run(global::app.goal.@this spec)
    {
        var engine = TestApp.Create("/app");
        var goal = await RealGoalLoad.ViaChannel(engine, spec);
        engine.Goal.Add(goal);
        var ctx = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, ctx);
        return (engine, ctx, result);
    }

    // Seed a list variable, then run one list action against it.
    static global::app.goal.@this OnList(string name, List<object?> items, global::app.goal.steps.step.actions.action.@this action, string actionText = "act")
        => Make.Goal("T",
            Make.Step($"set %{name}%",
                Make.Action("variable", "set", Make.Param("Name", name, "variable"), ("Value", items))),
            Make.Step(actionText, action));

    static async Task<global::app.type.list.@this?> StoredList(global::app.actor.context.@this ctx, string name)
        => (await ctx.Variable.GetValue(name)) as global::app.type.list.@this;

    // --- Add ---

    [Test]
    public async Task Add_CreatesNewList()
    {
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("add \"first\" to %myList%",
                Make.Action("list", "add", Make.Param("ListName", "myList", "variable"), ("Value", "first")))));
        await using var _ = engine;
        await result.IsSuccess();
        var list = await StoredList(ctx, "myList");
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That((await list.At(0)!.Value())?.ToString()).IsEqualTo("first");
    }

    [Test]
    public async Task Add_AppendsToExistingList()
    {
        var (engine, ctx, result) = await Run(OnList("myList", new List<object?> { "a", "b" },
            Make.Action("list", "add", Make.Param("ListName", "myList", "variable"), ("Value", "c"))));
        await using var _ = engine;
        await result.IsSuccess();
        var list = await StoredList(ctx, "myList");
        await Assert.That(list!.Count).IsEqualTo(3);
        await Assert.That((await list.At(2)!.Value())?.ToString()).IsEqualTo("c");
    }

    [Test]
    public async Task Add_InsertsAtIndex()
    {
        var (engine, ctx, result) = await Run(OnList("myList", new List<object?> { "a", "c" },
            Make.Action("list", "add", Make.Param("ListName", "myList", "variable"), ("Value", "b"), ("AtIndex", 1))));
        await using var _ = engine;
        await result.IsSuccess();
        var list = await StoredList(ctx, "myList");
        await Assert.That((await list!.At(1)!.Value())?.ToString()).IsEqualTo("b");
    }

    [Test]
    public async Task Add_List_FlattensIntoTarget()
    {
        // `add %b% to %a%` flattens b's items into a — observable as the flat count.
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %a%", Make.Action("variable", "set", Make.Param("Name", "a", "variable"), ("Value", new List<object?> { 10L, 20L }))),
            Make.Step("set %b%", Make.Action("variable", "set", Make.Param("Name", "b", "variable"), ("Value", new List<object?> { 50L, 60L }))),
            Make.Step("add %b% to %a%", Make.Action("list", "add", Make.Param("ListName", "a", "variable"), ("Value", "%b%")))));
        await using var _ = engine;
        await result.IsSuccess();
        var a = await StoredList(ctx, "a");
        await Assert.That(a!.Count).IsEqualTo(4);   // flat [10,20,50,60]
    }

    // --- Remove ---

    [Test]
    public async Task Remove_ByValue()
    {
        var (engine, ctx, result) = await Run(OnList("myList", new List<object?> { "a", "b", "c" },
            Make.Action("list", "remove", Make.Param("ListName", "myList", "variable"), ("Value", "b"))));
        await using var _ = engine;
        await result.IsSuccess();
        await Assert.That((await StoredList(ctx, "myList"))!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Remove_ByIndex()
    {
        var (engine, ctx, result) = await Run(OnList("myList", new List<object?> { "a", "b", "c" },
            Make.Action("list", "remove", Make.Param("ListName", "myList", "variable"), ("AtIndex", 0))));
        await using var _ = engine;
        await result.IsSuccess();
        await Assert.That((await (await StoredList(ctx, "myList"))!.At(0)!.Value())?.ToString()).IsEqualTo("b");
    }

    // --- Get ---

    [Test]
    public async Task Get_ReturnsItemAtIndex()
    {
        var (engine, _, result) = await Run(OnList("myList", new List<object?> { "a", "b", "c" },
            Make.Action("list", "get", Make.Param("ListName", "myList", "variable"), ("Index", 1))));
        await using var _e = engine;
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("b");
    }

    [Test]
    public async Task Get_OutOfRange_Fails()
    {
        var (engine, _, result) = await Run(OnList("myList", new List<object?> { "a" },
            Make.Action("list", "get", Make.Param("ListName", "myList", "variable"), ("Index", 5))));
        await using var _e = engine;
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
        await Assert.That(result.Error!.Message).Contains("out of range");
    }

    // --- Count ---

    [Test]
    public async Task Count_ReturnsCount()
    {
        var (engine, _, result) = await Run(OnList("myList", new List<object?> { "a", "b" },
            Make.Action("list", "count", Make.Param("ListName", "myList", "variable"))));
        await using var _e = engine;
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("2");
    }

    // --- Contains ---

    [Test]
    public async Task Contains_ReturnsTrue()
    {
        var (engine, _, result) = await Run(OnList("myList", new List<object?> { "a", "b" },
            Make.Action("list", "contains", Make.Param("ListName", "myList", "variable"), ("Value", "a"))));
        await using var _e = engine;
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
    }

    [Test]
    public async Task Contains_ReturnsFalse()
    {
        var (engine, _, result) = await Run(OnList("myList", new List<object?> { "a", "b" },
            Make.Action("list", "contains", Make.Param("ListName", "myList", "variable"), ("Value", "z"))));
        await using var _e = engine;
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
    }

    // --- First / Last ---

    [Test]
    public async Task First_ReturnsFirstItem()
    {
        var (engine, _, result) = await Run(OnList("myList", new List<object?> { "x", "y", "z" },
            Make.Action("list", "first", Make.Param("ListName", "myList", "variable"))));
        await using var _e = engine;
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("x");
    }

    [Test]
    public async Task Last_ReturnsLastItem()
    {
        var (engine, _, result) = await Run(OnList("myList", new List<object?> { "x", "y", "z" },
            Make.Action("list", "last", Make.Param("ListName", "myList", "variable"))));
        await using var _e = engine;
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("z");
    }

    // --- IndexOf ---

    [Test]
    public async Task IndexOf_FindsItem()
    {
        var (engine, _, result) = await Run(OnList("myList", new List<object?> { "a", "b", "c" },
            Make.Action("list", "indexof", Make.Param("ListName", "myList", "variable"), ("Value", "b"))));
        await using var _e = engine;
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("1");
    }

    // --- Sort ---

    [Test]
    public async Task Sort_SortsAscending()
    {
        var (engine, ctx, result) = await Run(OnList("myList", new List<object?> { "c", "a", "b" },
            Make.Action("list", "sort", Make.Param("ListName", "myList", "variable"), ("Descending", false))));
        await using var _ = engine;
        await result.IsSuccess();
        var list = await StoredList(ctx, "myList");
        await Assert.That((await list!.At(0)!.Value())?.ToString()).IsEqualTo("a");
        await Assert.That((await list.At(2)!.Value())?.ToString()).IsEqualTo("c");
    }

    // --- Join ---

    [Test]
    public async Task Join_JoinsWithSeparator()
    {
        var (engine, _, result) = await Run(OnList("myList", new List<object?> { "a", "b", "c" },
            Make.Action("list", "join", Make.Param("ListName", "myList", "variable"), ("Separator", "-"))));
        await using var _e = engine;
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("a-b-c");
    }

    // --- Split ---

    [Test]
    public async Task Split_SplitsString()
    {
        var (engine, _, result) = await Run(Make.Goal("T",
            Make.Step("split \"a,b,c\" by \",\"",
                Make.Action("list", "split", ("Value", "a,b,c"), ("Separator", ",")))));
        await using var _e = engine;
        await result.IsSuccess();
        var list = ((await result.Value()) as ListResult)?.value as global::app.type.list.@this;
        await Assert.That(list!.Count).IsEqualTo(3);
    }

    // --- Reverse ---

    [Test]
    public async Task Reverse_ReversesOrder()
    {
        var (engine, ctx, result) = await Run(OnList("myList", new List<object?> { 1L, 2L, 3L },
            Make.Action("list", "reverse", Make.Param("ListName", "myList", "variable"))));
        await using var _ = engine;
        await result.IsSuccess();
        var list = await StoredList(ctx, "myList");
        await Assert.That((await list!.At(0)!.Value())?.ToString()).IsEqualTo("3");
        await Assert.That((await list.At(2)!.Value())?.ToString()).IsEqualTo("1");
    }

    // --- Unique ---

    [Test]
    public async Task Unique_RemovesDuplicates()
    {
        var (engine, _, result) = await Run(OnList("myList", new List<object?> { "a", "b", "a", "c", "b" },
            Make.Action("list", "unique", Make.Param("ListName", "myList", "variable"))));
        await using var _e = engine;
        await result.IsSuccess();
        var list = ((await result.Value()) as ListResult)?.value as global::app.type.list.@this;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(3);
    }

    // --- Range ---

    [Test]
    public async Task Range_GeneratesSequence()
    {
        var (engine, _, result) = await Run(Make.Goal("T",
            Make.Step("range 1 to 5",
                Make.Action("list", "range", ("Start", 1), ("End", 5), ("Step", 1)))));
        await using var _e = engine;
        await result.IsSuccess();
        await Assert.That(((await result.Value()) as ListResult)!.count).IsEqualTo(5);
    }

    // --- Any ---

    [Test]
    [Arguments("==", "high", "high", "low", true)]
    [Arguments("==", "high", "low", "medium", false)]
    [Arguments("!=", "active", "active", "inactive", true)]
    public async Task Any_MatchesByOperator(string op, string needle, string a, string b, bool expected)
    {
        var key = op == "!=" ? "status" : "level";
        var items = new List<object?>
        {
            new Dictionary<string, object?> { [key] = a },
            new Dictionary<string, object?> { [key] = b },
        };
        var (engine, _, result) = await Run(OnList("items", items,
            Make.Action("list", "any", Make.Param("ListName", "items", "variable"),
                ("Key", key), ("Operator", op), ("Value", needle))));
        await using var _e = engine;
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo(expected ? "true" : "false");
    }

    [Test]
    public async Task Any_EmptyList_ReturnsFalse()
    {
        var (engine, _, result) = await Run(OnList("items", new List<object?>(),
            Make.Action("list", "any", Make.Param("ListName", "items", "variable"),
                ("Key", "level"), ("Operator", "=="), ("Value", "high"))));
        await using var _e = engine;
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
    }

    // --- Group ---

    [Test]
    public async Task Group_GroupsByKey()
    {
        var items = new List<object?>
        {
            new Dictionary<string, object?> { ["customer"] = "Alice", ["total"] = 50L },
            new Dictionary<string, object?> { ["customer"] = "Bob", ["total"] = 30L },
            new Dictionary<string, object?> { ["customer"] = "Alice", ["total"] = 20L },
        };
        var (engine, _, result) = await Run(OnList("orders", items,
            Make.Action("list", "group", Make.Param("ListName", "orders", "variable"), ("Key", "customer"))));
        await using var _e = engine;
        await result.IsSuccess();
        var groups = ((await result.Value()) as ListResult)?.value as global::app.type.list.@this;
        await Assert.That(groups!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Group_EmptyList_ReturnsEmpty()
    {
        var (engine, _, result) = await Run(OnList("items", new List<object?>(),
            Make.Action("list", "group", Make.Param("ListName", "items", "variable"), ("Key", "category"))));
        await using var _e = engine;
        await result.IsSuccess();
        var groups = ((await result.Value()) as ListResult)?.value as global::app.type.list.@this;
        await Assert.That(groups!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Group_MissingKey_GroupsUnderEmpty()
    {
        var items = new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "Alice" },
            new Dictionary<string, object?> { ["name"] = "Bob" },
        };
        var (engine, _, result) = await Run(OnList("items", items,
            Make.Action("list", "group", Make.Param("ListName", "items", "variable"), ("Key", "category"))));
        await using var _e = engine;
        await result.IsSuccess();
        var groups = ((await result.Value()) as ListResult)?.value as global::app.type.list.@this;
        await Assert.That(groups!.Count).IsEqualTo(1);
        await Assert.That((await ((global::app.type.dict.@this)(await groups.At(0)!.Value())!).Get("key")!.Value())?.ToString()).IsEqualTo("");
    }

    // --- Flatten ---

    [Test]
    public async Task Flatten_FlattensNestedLists()
    {
        var nested = new List<object?> { 1L, new List<object?> { 2L, 3L }, new List<object?> { 4L, new List<object?> { 5L } } };
        var (engine, _, result) = await Run(OnList("myList", nested,
            Make.Action("list", "flatten", Make.Param("ListName", "myList", "variable"))));
        await using var _e = engine;
        await result.IsSuccess();
        var flat = ((await result.Value()) as ListResult)?.value as global::app.type.list.@this;
        await Assert.That(flat!.Count).IsEqualTo(5);
        // Assert the flattened order — forces the recursive-descent arm (nested lists lifted).
        var vals = new List<string?>();
        for (int i = 0; i < (int)flat.Count; i++) vals.Add((await flat.At(i)!.Value())?.ToString());
        await Assert.That(string.Join(",", vals)).IsEqualTo("1,2,3,4,5");
    }

    // --- Set (list.set) ---

    [Test]
    public async Task Set_ValidIndex_UpdatesElement()
    {
        var (engine, ctx, result) = await Run(OnList("myList", new List<object?> { "a", "b", "c" },
            Make.Action("list", "set", Make.Param("ListName", "myList", "variable"), ("Index", 1), ("Value", "replaced"))));
        await using var _ = engine;
        await result.IsSuccess();
        await Assert.That((await (await StoredList(ctx, "myList"))!.At(1)!.Value())?.ToString()).IsEqualTo("replaced");
    }

    [Test]
    public async Task Set_FirstElement_UpdatesCorrectly()
    {
        var (engine, ctx, result) = await Run(OnList("myList", new List<object?> { "old", "keep" },
            Make.Action("list", "set", Make.Param("ListName", "myList", "variable"), ("Index", 0), ("Value", "new"))));
        await using var _ = engine;
        await result.IsSuccess();
        var list = await StoredList(ctx, "myList");
        await Assert.That((await list!.At(0)!.Value())?.ToString()).IsEqualTo("new");
        await Assert.That((await list.At(1)!.Value())?.ToString()).IsEqualTo("keep");
    }

    [Test]
    public async Task Set_OutOfBounds_Fails()
    {
        var (engine, _, result) = await Run(OnList("myList", new List<object?> { "a", "b" },
            Make.Action("list", "set", Make.Param("ListName", "myList", "variable"), ("Index", 5), ("Value", "x"))));
        await using var _e = engine;
        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("out of range");
    }

    [Test]
    public async Task Set_NegativeIndex_Fails()
    {
        var (engine, _, result) = await Run(OnList("myList", new List<object?> { "a" },
            Make.Action("list", "set", Make.Param("ListName", "myList", "variable"), ("Index", -1), ("Value", "x"))));
        await using var _e = engine;
        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("out of range");
    }

    [Test]
    public async Task Set_NotAList_Fails()
    {
        var (engine, _, result) = await Run(Make.Goal("T",
            Make.Step("set %myList% = \"not a list\"",
                Make.Action("variable", "set", Make.Param("Name", "myList", "variable"), ("Value", "not a list"))),
            Make.Step("set index 0",
                Make.Action("list", "set", Make.Param("ListName", "myList", "variable"), ("Index", 0), ("Value", "x")))));
        await using var _e = engine;
        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("not a list");
    }

    [Test]
    public async Task Set_NonexistentVariable_Fails()
    {
        var (engine, _, result) = await Run(Make.Goal("T",
            Make.Step("set index 0 of %missing%",
                Make.Action("list", "set", Make.Param("ListName", "missing", "variable"), ("Index", 0), ("Value", "x")))));
        await using var _e = engine;
        await result.IsFailure();
    }

    [Test]
    public async Task Set_ToNull_Succeeds()
    {
        var (engine, ctx, result) = await Run(OnList("myList", new List<object?> { "a", "b" },
            Make.Action("list", "set", Make.Param("ListName", "myList", "variable"), ("Index", 0), ("Value", null))));
        await using var _ = engine;
        await result.IsSuccess();
        var list = await StoredList(ctx, "myList");
        await Assert.That(await (await list!.At(0)!.Value())!.IsEmpty()).IsTrue();
    }
}
