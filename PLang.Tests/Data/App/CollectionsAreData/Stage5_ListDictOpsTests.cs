using app.variable;
using ListV = global::app.type.item.list.@this;
using DictV = global::app.type.item.dict.@this;
using Op = global::app.module.action.condition.Operator;
using Where = global::app.module.action.list.Where;
using Sort = global::app.module.action.list.Sort;
using Unique = global::app.module.action.list.Unique;
using Group = global::app.module.action.list.Group;

namespace PLang.Tests.App.CollectionsAreData;

// Stage 5 — list/dict ops as exposure. `where` is a dict+list capability; sort/group
// stay list-only and route through the one typed-compare path (Stage 4). No
// test-designer C# batch for this stage — these pin the new handler behavior so the
// thin-dispatch handlers are verifiable without the (LLM-built) PLang layer.
public class Stage5_ListDictOpsTests
{
    private global::app.@this _app = null!;
    [Before(Test)] public void Setup() => _app = global::PLang.Tests.TestApp.Create("/app");
    [After(Test)] public async Task TearDown() { await _app.DisposeAsync(); }
    private (global::app.actor.context.@this ctx, Variables vars) Ctx() => (_app.User.Context, _app.User.Context.Variable);
    private Data D(object? v) => _app.Data("", v);
    private DictV Person(string field, object? val) { var d = new DictV(_app.User.Context); d.Set(_app.Data(field, val)); return d; }

    private Where WhereAction(global::app.actor.context.@this ctx, string var, string field, string op, object? value)
        => new(ctx) {  ListName = new app.variable.@this(var), Field = new global::app.data.@this<global::app.type.item.text.@this>("", field, context: ctx),
                   Operator = new global::app.data.@this<global::app.type.item.choice.@this<Op>>("", new Op(op), context: ctx), Value = D(value) };

    [Test]
    public async Task WhereOnList_FiltersByPredicate()
    {
        var (ctx, vars) = Ctx();
        var users = new ListV(ctx);
        users.Add(_app.Data("", Person("age", 25L)));
        users.Add(_app.Data("", Person("age", 15L)));
        users.Add(_app.Data("", Person("age", 40L)));
        vars.Set("users", users);

        var result = await WhereAction(ctx, "users", "age", ">", 20L).Run();
        await result.IsSuccess();
        var filtered = (ListV)(await result.Value())!;
        await Assert.That(filtered.Count).IsEqualTo(2);
        await Assert.That(((global::app.type.item.number.@this)(await (await filtered.At(0)!.Get("age")).Value())!).Clr<long>()).IsEqualTo(25L);
    }

    [Test]
    public async Task WhereOnDict_KeepsOrDrops()
    {
        var (ctx, vars) = Ctx();
        vars.Set("user", Person("age", 25L));
        var kept = await WhereAction(ctx, "user", "age", ">", 20L).Run();
        await kept.IsSuccess();
        await Assert.That((await kept.Value())).IsTypeOf<DictV>();

        vars.Set("user2", Person("age", 10L));
        var dropped = await WhereAction(ctx, "user2", "age", ">", 20L).Run();
        await dropped.IsSuccess();
        await Assert.That(await (await dropped.Value())!.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task WhereOnApex_Errors()
    {
        var (ctx, vars) = Ctx();
        vars.Set("x", 5L);
        var result = await WhereAction(ctx, "x", "age", ">", 20L).Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("WhereOnApex");
    }

    [Test]
    public async Task SortByField_OrdersNumerically()
    {
        var (ctx, vars) = Ctx();
        var people = new ListV(ctx);
        people.Add(_app.Data("", Person("age", 30L)));
        people.Add(_app.Data("", Person("age", 10L)));
        people.Add(_app.Data("", Person("age", 20L)));
        vars.Set("people", people);

        var action = new Sort(ctx) { ListName = new app.variable.@this("people"), By = new global::app.data.@this<global::app.type.item.text.@this>("", "age", context: ctx) };
        await (await action.Run()).IsSuccess();
        var sorted = (ListV)(await (await vars.Get("people")).Value())!;
        await Assert.That(((global::app.type.item.number.@this)(await (await sorted.At(0)!.Get("age")).Value())!).Clr<long>()).IsEqualTo(10L);
        await Assert.That(((global::app.type.item.number.@this)(await (await sorted.At(2)!.Get("age")).Value())!).Clr<long>()).IsEqualTo(30L);
    }

    [Test]
    public async Task SortOnListOfDict_ReturnsError()
    {
        // dict is equality-only — sorting a list of dicts (no field) is unorderable. In PLang
        // that's an EXPECTED data condition, so sort RETURNS a Data error (it does not throw —
        // a thrown exception would escape the `on error` handler pipeline).
        var (ctx, vars) = Ctx();
        var dicts = new ListV(ctx);
        dicts.Add(_app.Data("", Person("city", "Reyk")));
        dicts.Add(_app.Data("", Person("city", "Oslo")));
        vars.Set("dicts", dicts);
        var action = new Sort(ctx) { ListName = new app.variable.@this("dicts") };
        var result = await action.Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("order");
    }

    [Test]
    public async Task UniqueUsesCompareEquality()
    {
        var (ctx, vars) = Ctx();
        var values = new ListV(ctx);
        values.Add(_app.Data("", Person("city", "Reyk")));
        values.Add(_app.Data("", Person("city", "Reyk")));   // structurally equal
        values.Add(_app.Data("", Person("city", "Oslo")));
        vars.Set("values", values);
        var action = new Unique(ctx) { ListName = new app.variable.@this("values") };
        var result = await action.Run();
        await result.IsSuccess();
        await Assert.That((await result.Value())!.value as ListV).IsNotNull();
        await Assert.That(((ListV)(await result.Value())!.value!).Count).IsEqualTo(2);
    }

    [Test]
    public async Task GroupByField_BucketsAreNavigableLists()
    {
        var (ctx, vars) = Ctx();
        var people = new ListV(ctx);
        people.Add(_app.Data("", Person("city", "Reyk")));
        people.Add(_app.Data("", Person("city", "Oslo")));
        people.Add(_app.Data("", Person("city", "Reyk")));
        vars.Set("people", people);
        var action = new Group(ctx) { ListName = new app.variable.@this("people"), Key = new global::app.data.@this<global::app.type.item.text.@this>("", "city", context: ctx) };
        var result = await action.Run();
        await result.IsSuccess();
        var groups = (ListV)(await result.Value())!.value!;
        await Assert.That(groups.Count).IsEqualTo(2);
        var reyk = (DictV)(await groups.At(0)!.Value())!;
        await Assert.That((await (reyk.Get("key"))!.Value())?.ToString()).IsEqualTo("Reyk");
        await Assert.That(((ListV)(await (reyk.Get("items"))!.Value())!).Count).IsEqualTo(2); // navigable bucket
    }
}
