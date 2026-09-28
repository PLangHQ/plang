using app.actor.context;
using app;
using app.type.item.variable;
using app.module.action.list;

namespace PLang.Tests.App.actions.list;

public class ListTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = TestApp.Create("/app");
        return (app.User.Context, app.User.Context.Variable);
    }

    // --- Add ---

    // list.add stores the WHOLE Data — lists carry Data objects, not raw values,
    // so each element keeps its name/type/context. Readers (list.Navigate,
    // EnumerateItems) unwrap on access; low-level tests look at the Data wrapper.
    private static object? Unwrap(object? slot) =>
        slot is global::app.data.@this d ? (d.Peek()) : slot;

    [Test]
    public async Task Add_CreatesNewList()
    {
        var (context, memory) = CreateContext();

        var action = new Add(context) { ListName = new app.type.item.variable.@this("myList"), Value = new global::app.data.@this("", "first", context: context)};
        var result = await action.Start();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.item.list.@this;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That((await list.At(0, global::PLang.Tests.TestApp.SharedContext)!.Value())?.ToString()).IsEqualTo("first");
    }

    [Test]
    public async Task Add_AppendsToExistingList()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Add(context) { ListName = new app.type.item.variable.@this("myList"), Value = new global::app.data.@this("", "c", context: context)};
        var result = await action.Start();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.item.list.@this;
        await Assert.That(list!.Count).IsEqualTo(3);
        await Assert.That((await list.At(2, global::PLang.Tests.TestApp.SharedContext)!.Value())?.ToString()).IsEqualTo("c");
    }

    [Test]
    public async Task Add_InsertsAtIndex()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "c" });

        var action = new Add(context) { ListName = new app.type.item.variable.@this("myList"), Value = new global::app.data.@this("", "b", context: context), AtIndex = (global::app.type.item.number.@this)1 };
        var result = await action.Start();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.item.list.@this;
        await Assert.That((await list!.At(1, global::PLang.Tests.TestApp.SharedContext)!.Value())?.ToString()).IsEqualTo("b");
    }

    [Test]
    public async Task Add_List_SharesSourceInstance_ReferenceSemantics()
    {
        // Collections are reference semantics: `add %b% to %a%` stores %b%'s
        // list INSTANCE (the entry mints its own Data pointing at it, nothing
        // copied) — in-place mutation of either side shows through both names.
        var (context, memory) = CreateContext();
        var aList = new global::app.type.item.list.@this();
        aList.Add(new global::app.data.@this("", 10L, context: context)); aList.Add(new global::app.data.@this("", 20L, context: context));
        var bList = new global::app.type.item.list.@this();
        bList.Add(new global::app.data.@this("", 50L, context: context)); bList.Add(new global::app.data.@this("", 60L, context: context));
        memory.Set("a", aList);
        memory.Set("b", bList);

        var action = new Add(context) { ListName = new app.type.item.variable.@this("a"), Value = await memory.Get("b") };
        await (await action.Start()).IsSuccess();

        var a = (await memory.GetValue("a")) as global::app.type.item.list.@this;
        var b = (await memory.GetValue("b")) as global::app.type.item.list.@this;
        await Assert.That(a!.Count).IsEqualTo(4);   // flat [10,20,50,60]

        // write-through: mutate the leaf in %a% that came from %b% → visible via %b%.
        a.SetAt(2, new global::app.data.@this("", 99L, context: context));
        await Assert.That((await a.At(2, global::PLang.Tests.TestApp.SharedContext)!.Value())?.ToString()).IsEqualTo("99");
        await Assert.That((await b!.At(0, global::PLang.Tests.TestApp.SharedContext)!.Value())?.ToString()).IsEqualTo("99");

        // read-view: mutate %b% → %a% flattens through the shared row and tracks it.
        b.Add(new global::app.data.@this("", 70L, context: context));
        await Assert.That(a.Count).IsEqualTo(5);
    }

    // --- Remove ---

    [Test]
    public async Task Remove_ByValue()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Remove(context) { ListName = new app.type.item.variable.@this("myList"), Value = new global::app.data.@this("", "b", context: context)};
        var result = await action.Start();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.item.list.@this;
        await Assert.That(list!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Remove_ByIndex()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Remove(context) { ListName = new app.type.item.variable.@this("myList"), AtIndex = (global::app.type.item.number.@this)0 };
        var result = await action.Start();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.item.list.@this;
        await Assert.That((await list!.At(0, global::PLang.Tests.TestApp.SharedContext)!.Value())?.ToString()).IsEqualTo("b");
    }

    // --- Get ---

    [Test]
    public async Task Get_ReturnsItemAtIndex()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Get(context) { ListName = new app.type.item.variable.@this("myList"), Index = (global::app.type.item.number.@this)1 };
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("b");
    }

    [Test]
    public async Task Get_OutOfRange_Fails()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a" });

        var action = new Get(context) { ListName = new app.type.item.variable.@this("myList"), Index = (global::app.type.item.number.@this)5 };
        var result = await action.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("IndexOutOfRange");
        await Assert.That(result.Error!.Message).Contains("out of range");
    }

    [Test]
    public async Task Get_WithNoIndex_IsAnError_NotACrash()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a" });

        var action = new Get(context) { ListName = new app.type.item.variable.@this("myList"), Index = global::app.data.@this<global::app.type.item.number.@this>.Uninitialized("index") };
        var result = await action.Start();

        // the index's own answer — it never resolved to a number
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NumberConversionFailed");
    }

    [Test]
    public async Task Count_OfAText_IsNotAList()
    {
        var (context, memory) = CreateContext();
        memory.Set("word", "abc");

        var result = await new Count(context) { ListName = new app.type.item.variable.@this("word") }.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotA");
        await Assert.That(result.Error!.Message).Contains("not a list");
    }

    // --- Count ---

    [Test]
    public async Task Count_ReturnsList()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Count(context) { ListName = new app.type.item.variable.@this("myList") };
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("2");
    }

    // --- Contains ---

    [Test]
    public async Task Contains_ReturnsTrue()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Contains(context) { ListName = new app.type.item.variable.@this("myList"), Value = new global::app.data.@this("", "a", context: context)};
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
    }

    [Test]
    public async Task Contains_ReturnsFalse()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b" });

        var action = new Contains(context) { ListName = new app.type.item.variable.@this("myList"), Value = new global::app.data.@this("", "z", context: context)};
        var result = await action.Start();

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
    }

    // --- First / Last ---

    [Test]
    public async Task First_ReturnsFirstItem()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "x", "y", "z" });

        var action = new First(context) { ListName = new app.type.item.variable.@this("myList") };
        var result = await action.Start();

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("x");
    }

    [Test]
    public async Task Last_ReturnsLastItem()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "x", "y", "z" });

        var action = new Last(context) { ListName = new app.type.item.variable.@this("myList") };
        var result = await action.Start();

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("z");
    }

    // --- IndexOf ---

    [Test]
    public async Task IndexOf_FindsItem()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new IndexOf(context) { ListName = new app.type.item.variable.@this("myList"), Value = new global::app.data.@this("", "b", context: context)};
        var result = await action.Start();

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("1");
    }

    // --- Sort ---

    [Test]
    public async Task Sort_SortsAscending()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "c", "a", "b" });

        var action = new Sort(context) { ListName = new app.type.item.variable.@this("myList"), Descending = (global::app.type.item.@bool.@this)false };
        var result = await action.Start();

        await result.IsSuccess();
        var list = (await memory.GetValue("myList")) as global::app.type.item.list.@this;
        await Assert.That((await list!.At(0, global::PLang.Tests.TestApp.SharedContext)!.Value())?.ToString()).IsEqualTo("a");
        await Assert.That((await list.At(2, global::PLang.Tests.TestApp.SharedContext)!.Value())?.ToString()).IsEqualTo("c");
    }

    // --- Join ---

    [Test]
    public async Task Join_JoinsWithSeparator()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "c" });

        var action = new Join(context) { ListName = new app.type.item.variable.@this("myList"), Separator = (global::app.type.item.text.@this)"-" };
        var result = await action.Start();

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("a-b-c");
    }

    // --- Split ---

    [Test]
    public async Task Split_SplitsString()
    {
        var (context, _) = CreateContext();

        var action = new Split(context) { Value = (global::app.type.item.text.@this)"a,b,c", Separator = (global::app.type.item.text.@this)"," };
        var result = await action.Start();

        await result.IsSuccess();
        var list = (await result.Value()) as global::app.type.item.list.@this;
        await Assert.That(list!.Count).IsEqualTo(3);
    }

    // --- Reverse ---

    [Test]
    public async Task Reverse_ReversesOrder()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { 1, 2, 3 });

        var action = new Reverse(context) { ListName = new app.type.item.variable.@this("myList") };
        var result = await action.Start();

        var list = (await memory.GetValue("myList")) as global::app.type.item.list.@this;
        await Assert.That((await list!.At(0, global::PLang.Tests.TestApp.SharedContext)!.Value())?.ToString()).IsEqualTo("3");
        await Assert.That((await list.At(2, global::PLang.Tests.TestApp.SharedContext)!.Value())?.ToString()).IsEqualTo("1");
    }

    // --- Unique ---

    [Test]
    public async Task Unique_RemovesDuplicates()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a", "b", "a", "c", "b" });

        var action = new Unique(context) { ListName = new app.type.item.variable.@this("myList") };
        var result = await action.Start();

        var list = (await result.Value()) as global::app.type.item.list.@this;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(3);
        var values = list.Items(global::PLang.Tests.TestApp.SharedContext).Select(d => d.Peek()?.ToString()).ToList();
        await Assert.That(values).Contains("a");
        await Assert.That(values).Contains("b");
        await Assert.That(values).Contains("c");
    }

    // --- Range ---

    [Test]
    public async Task Range_GeneratesSequence()
    {
        var (context, _) = CreateContext();

        var action = new global::app.module.action.list.Range(context) { From = (global::app.type.item.number.@this)1, To = (global::app.type.item.number.@this)5, Step = (global::app.type.item.number.@this)1 };
        var result = await action.Start();

        var listResult = (await result.Value()) as global::app.type.item.list.@this;
        await Assert.That(listResult!.CountRaw).IsEqualTo(5);
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

        var action = new Any(context) { ListName = new app.type.item.variable.@this("items"),
            Key = (global::app.type.item.text.@this)"level",
            Operator = (global::app.type.item.choice.@this<global::app.data.Operator>)new global::app.data.Operator("=="),
            Value = new global::app.data.@this("", "high", context: context)
        };
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
    }

    // --- Where: the value answers ---

    private static global::app.type.item.choice.@this<global::app.data.Operator> Op(string op)
        => (global::app.type.item.choice.@this<global::app.data.Operator>)new global::app.data.Operator(op);

    [Test]
    public async Task Where_OnAList_KeepsTheElementsWhoseFieldHolds()
    {
        var (context, memory) = CreateContext();
        memory.Set("users", new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "a", ["age"] = 30L },
            new Dictionary<string, object?> { ["name"] = "b", ["age"] = 10L },
        });

        var result = await new Where(context) { ListName = new app.type.item.variable.@this("users"),
            Field = (global::app.type.item.text.@this)"age", Operator = Op(">"),
            Value = new global::app.data.@this("", 20L, context: context) }.Start();

        await result.IsSuccess();
        var kept = (global::app.type.item.list.@this)(await result.Value())!;
        await Assert.That(kept.CountRaw).IsEqualTo(1);
        await Assert.That((await (await kept.First(context)!.Get("name")).Value())?.ToString()).IsEqualTo("a");
    }

    // Only the dict that has the field and holds is kept: a scalar or a nested list has no such field — it
    // doesn't hold, like a dict without the key (never an error, never "kept because it's a list").
    [Test]
    public async Task Where_And_Any_OverAMixedList_OnlyAFieldThatHoldsCounts()
    {
        var (context, memory) = CreateContext();
        memory.Set("mixed", new List<object?>
        {
            new Dictionary<string, object?> { ["age"] = 30L },
            5L,
            new List<object?> { new Dictionary<string, object?> { ["age"] = 99L } },
        });

        var kept = await new Where(context) { ListName = new app.type.item.variable.@this("mixed"),
            Field = (global::app.type.item.text.@this)"age", Operator = Op("=="),
            Value = new global::app.data.@this("", 30L, context: context) }.Start();
        var none = await new Any(context) { ListName = new app.type.item.variable.@this("mixed"),
            Key = (global::app.type.item.text.@this)"age", Operator = Op("=="),
            Value = new global::app.data.@this("", 99L, context: context) }.Start();

        await kept.IsSuccess();
        await Assert.That(((global::app.type.item.list.@this)(await kept.Value())!).CountRaw).IsEqualTo(1);
        await none.IsSuccess();
        await Assert.That((await none.Value())?.ToString()).IsEqualTo("false");
    }

    private async Task<global::app.data.@this> WhereOf(global::app.actor.context.@this context, string subject, string field, string op, object? value)
        => await new Where(context) { ListName = new app.type.item.variable.@this(subject),
            Field = (global::app.type.item.text.@this)field, Operator = Op(op),
            Value = new global::app.data.@this("", value, context: context) }.Start();

    // A field no item has is a misspelling: an error naming it and the fields the items have — for every
    // operator, and for any too.
    [Test]
    public async Task Where_AFieldNoItemHas_IsAnError_NamingItAndTheFieldsThereAre()
    {
        var (context, memory) = CreateContext();
        memory.Set("users", new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "a", ["age"] = 30L },
            new Dictionary<string, object?> { ["name"] = "b" },
        });

        foreach (var op in new[] { "==", ">", "contains" })
        {
            var result = await WhereOf(context, "users", "agee", op, 30L);
            await result.IsFailure();
            await Assert.That(result.Error!.Key).IsEqualTo("FieldNotFound");
            await Assert.That(result.Error.Message).Contains("agee");
            await Assert.That(result.Error.Message).Contains("name, age");
        }
        var any = await new Any(context) { ListName = new app.type.item.variable.@this("users"),
            Key = (global::app.type.item.text.@this)"agee", Operator = Op("=="),
            Value = new global::app.data.@this("", 30L, context: context) }.Start();
        await any.IsFailure();
        await Assert.That(any.Error!.Key).IsEqualTo("FieldNotFound");
    }

    // A field only some items have filters: an item without it doesn't match an order or ==, and matches is null.
    [Test]
    public async Task Where_AFieldSomeItemsHave_FiltersByTheItemsThatHaveIt()
    {
        var (context, memory) = CreateContext();
        memory.Set("users", new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "a", ["age"] = 30L },
            new Dictionary<string, object?> { ["name"] = "b" },
        });

        async Task<List<string?>> Names(global::app.data.@this result)
        {
            await result.IsSuccess();
            var names = new List<string?>();
            foreach (var user in ((global::app.type.item.list.@this)(await result.Value())!).Items(context))
                names.Add((await (await user.Get("name")).Value())?.ToString());
            return names;
        }

        await Assert.That(await Names(await WhereOf(context, "users", "age", ">", 20L))).IsEquivalentTo(new[] { "a" });
        await Assert.That(await Names(await WhereOf(context, "users", "age", "<", 99L))).IsEquivalentTo(new[] { "a" });
        await Assert.That(await Names(await WhereOf(context, "users", "age", "==", 30L))).IsEquivalentTo(new[] { "a" });
        await Assert.That(await Names(await WhereOf(context, "users", "age", "==", null))).IsEquivalentTo(new[] { "b" });
    }

    // Only an item's own missing field is no match; the value it's compared to is the developer's own variable,
    // and ordering by one that holds nothing is an error — never a quietly empty list.
    [Test]
    public async Task Where_OrderedByAnUnsetVariable_IsAnError()
    {
        var (context, memory) = CreateContext();
        memory.Set("users", new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "a", ["age"] = 30L },
            new Dictionary<string, object?> { ["name"] = "b" },
        });

        var goal = await RealGoalLoad.ViaChannel(context.App, Make.Goal("WhereUnset",
            Make.Step("list.where %users% where age > %limit%",
                Make.Action("list", "where", Make.Param("ListName", "users", "variable"), ("Field", "age"),
                    ("Operator", ">"), Make.Param("Value", "%limit%", "variable")))));
        var result = await goal.Step[0].Start(context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("VariableNotFound");
        await Assert.That(result.Error.Message).Contains("limit");
    }

    [Test]
    public async Task Where_OnAnEmptyList_KeepsNothing_AndOnADictWithoutTheField_IsAnError()
    {
        var (context, memory) = CreateContext();
        memory.Set("nobody", new List<object?>());
        memory.Set("user", new Dictionary<string, object?> { ["age"] = 30L });

        var empty = await WhereOf(context, "nobody", "agee", "==", 30L);
        await empty.IsSuccess();
        await Assert.That(((global::app.type.item.list.@this)(await empty.Value())!).CountRaw).IsEqualTo(0);

        var typo = await WhereOf(context, "user", "agee", "==", 30L);
        await typo.IsFailure();
        await Assert.That(typo.Error!.Key).IsEqualTo("FieldNotFound");
        await Assert.That(typo.Error.Message).Contains("age");
    }

    [Test]
    public async Task Where_OnADict_KeepsItOrNothing()
    {
        var (context, memory) = CreateContext();
        memory.Set("user", new Dictionary<string, object?> { ["age"] = 30L });

        var kept = await new Where(context) { ListName = new app.type.item.variable.@this("user"),
            Field = (global::app.type.item.text.@this)"age", Operator = Op(">"),
            Value = new global::app.data.@this("", 20L, context: context) }.Start();
        var dropped = await new Where(context) { ListName = new app.type.item.variable.@this("user"),
            Field = (global::app.type.item.text.@this)"age", Operator = Op("<"),
            Value = new global::app.data.@this("", 20L, context: context) }.Start();

        await Assert.That(await kept.Value()).IsTypeOf<global::app.type.item.dict.@this>();
        await Assert.That((await dropped.Value()) is null or { IsNull: true }).IsTrue();
    }

    [Test]
    public async Task Where_OnAScalar_HasNoFields()
    {
        var (context, memory) = CreateContext();
        memory.Set("n", 5L);

        var result = await new Where(context) { ListName = new app.type.item.variable.@this("n"),
            Field = (global::app.type.item.text.@this)"age", Operator = Op(">"),
            Value = new global::app.data.@this("", 20L, context: context) }.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("WhereOnApex");
    }

    [Test]
    public async Task Remove_OutOfRange_IsIndexOutOfRange()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { "a" });

        var result = await new Remove(context) { ListName = new app.type.item.variable.@this("myList"),
            Value = new global::app.data.@this("", null, context: context), AtIndex = (global::app.type.item.number.@this)5 }.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("IndexOutOfRange");
    }

    [Test]
    public async Task Sort_ByAFieldThatDidNotResolve_IsItsOwnAnswer_NotASortByValue()
    {
        var (context, memory) = CreateContext();
        memory.Set("myList", new List<object?> { 2L, 1L });

        // `sort %myList% by %field%` with %field% never set: the given `by` fails to resolve
        var result = await TestAction.Create("list", "sort", ("listName", "%myList%"), ("by", "%field%")).Start(context);

        await result.IsFailure();
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

        var action = new Any(context) { ListName = new app.type.item.variable.@this("items"),
            Key = (global::app.type.item.text.@this)"level",
            Operator = (global::app.type.item.choice.@this<global::app.data.Operator>)new global::app.data.Operator("=="),
            Value = new global::app.data.@this("", "high", context: context)
        };
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
    }

    [Test]
    public async Task Any_EmptyList_ReturnsFalse()
    {
        var (context, memory) = CreateContext();
        memory.Set("items", new List<object?>());

        var action = new Any(context) { ListName = new app.type.item.variable.@this("items"),
            Key = (global::app.type.item.text.@this)"level",
            Operator = (global::app.type.item.choice.@this<global::app.data.Operator>)new global::app.data.Operator("=="),
            Value = new global::app.data.@this("", "high", context: context)
        };
        var result = await action.Start();

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

        var action = new Any(context) { ListName = new app.type.item.variable.@this("items"),
            Key = (global::app.type.item.text.@this)"status",
            Operator = (global::app.type.item.choice.@this<global::app.data.Operator>)new global::app.data.Operator("!="),
            Value = new global::app.data.@this("", "active", context: context)
        };
        var result = await action.Start();

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

        var action = new Group(context) { ListName = new app.type.item.variable.@this("orders"), Key = (global::app.type.item.text.@this)"customer" };
        var result = await action.Start();

        await result.IsSuccess();
        var groups = (await result.Value()) as global::app.type.item.list.@this;
        await Assert.That(groups).IsNotNull();
        await Assert.That(groups!.Count).IsEqualTo(2);

        await Assert.That(BucketCount(groups, "Alice")).IsEqualTo(2);
        await Assert.That(BucketCount(groups, "Bob")).IsEqualTo(1);
    }

    // Helper: find a group bucket by key and return its items list count.
    private static int BucketCount(global::app.type.item.list.@this groups, string key)
    {
        foreach (var b in groups.Items(global::PLang.Tests.TestApp.SharedContext))
        {
            var d = (global::app.type.item.dict.@this)(b.Peek())!;
            if (((d.Get("key", global::PLang.Tests.TestApp.SharedContext)).Peek())?.ToString() == key)
                return (int)((global::app.type.item.list.@this)((d.Get("items", global::PLang.Tests.TestApp.SharedContext))!.Peek())!).Count;
        }
        return -1;
    }

    [Test]
    public async Task Group_EmptyList_ReturnsEmpty()
    {
        var (context, memory) = CreateContext();
        memory.Set("items", new List<object?>());

        var action = new Group(context) { ListName = new app.type.item.variable.@this("items"), Key = (global::app.type.item.text.@this)"category" };
        var result = await action.Start();

        await result.IsSuccess();
        var groups = (await result.Value()) as global::app.type.item.list.@this;
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

        var action = new Group(context) { ListName = new app.type.item.variable.@this("items"), Key = (global::app.type.item.text.@this)"category" };
        var result = await action.Start();

        await result.IsSuccess();
        var groups = (await result.Value()) as global::app.type.item.list.@this;
        // All items grouped under empty key since "category" doesn't exist
        await Assert.That(groups!.Count).IsEqualTo(1);
        await Assert.That((await ((global::app.type.item.dict.@this)(await groups.At(0, global::PLang.Tests.TestApp.SharedContext)!.Value())!).Get("key", global::PLang.Tests.TestApp.SharedContext)!.Value())?.ToString()).IsEqualTo("");
    }

    // --- Flatten ---

    [Test]
    public async Task Flatten_FlattensNestedLists()
    {
        var (context, memory) = CreateContext();
        var nested = new List<object?> { 1, new List<object?> { 2, 3 }, new List<object?> { 4, new List<object?> { 5 } } };
        memory.Set("myList", nested);

        var action = new Flatten(context) { ListName = new app.type.item.variable.@this("myList") };
        var result = await action.Start();

        var listResult = (await result.Value()) as global::app.type.item.list.@this;
        await Assert.That(listResult!.CountRaw).IsEqualTo(5);
    }
}
