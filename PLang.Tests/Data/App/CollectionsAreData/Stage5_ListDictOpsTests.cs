using app.type.item.variable;
using ListV = global::app.type.item.list.@this;
using DictV = global::app.type.item.dict.@this;
using Op = global::app.data.Operator;

namespace PLang.Tests.App.CollectionsAreData;

// Stage 5 — list/dict ops as exposure. `where` is a dict+list capability; sort/group
// stay list-only and route through the one typed-compare path (Stage 4). These pin the
// DOOR behavior (item.Where / list.Sort / list.Unique / list.Group) that list.query now
// drives — the four list.where/sort/group/unique actions were replaced by list.query, so
// the proofs target the doors directly, same data, same assertions.
public class Stage5_ListDictOpsTests
{
    private global::app.@this _app = null!;
    [Before(Test)] public void Setup() => _app = new global::app.@this("/app").Testing();
    [After(Test)] public async Task TearDown() { await _app.DisposeAsync(); }
    private global::app.actor.context.@this Ctx() => _app.actor.list.User.Context;
    private Data D(object? v) => _app.Data("", v);
    private DictV Person(string field, object? val) { var d = new DictV(); d.Set(_app.Data(field, val)); return d; }
    private global::app.type.item.text.@this Text(string s) => new(s);
    private global::app.type.item.@bool.@this Desc(bool b) => new(b);

    [Test]
    public async Task WhereOnList_FiltersByPredicate()
    {
        var ctx = Ctx();
        var users = new ListV();
        users.Add(_app.Data("", Person("age", 25L)));
        users.Add(_app.Data("", Person("age", 15L)));
        users.Add(_app.Data("", Person("age", 40L)));

        var result = await users.Where(Text("age"), new Op(">"), D(20L), ctx);
        await result.IsSuccess();
        var filtered = (ListV)(await result.Value())!;
        await Assert.That(filtered.Count).IsEqualTo(2);
        await Assert.That(((global::app.type.item.number.@this)(await (await filtered.At(0, ctx)!.Get("age")).Value())!).Clr<long>()).IsEqualTo(25L);
    }

    [Test]
    public async Task WhereOnDict_KeepsOrDrops()
    {
        var ctx = Ctx();
        var kept = await Person("age", 25L).Where(Text("age"), new Op(">"), D(20L), ctx);
        await kept.IsSuccess();
        await Assert.That((await kept.Value())).IsTypeOf<DictV>();

        var dropped = await Person("age", 10L).Where(Text("age"), new Op(">"), D(20L), ctx);
        await dropped.IsSuccess();
        await Assert.That(await (await dropped.Value())!.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task WhereOnApex_Errors()
    {
        var ctx = Ctx();
        var apex = (await _app.Data("", 5L).Value())!;      // a number apex — no fields to scope into
        var result = await apex.Where(Text("age"), new Op(">"), D(20L), ctx);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("WhereOnApex");
    }

    [Test]
    public async Task SortByField_OrdersNumerically()
    {
        var ctx = Ctx();
        var people = new ListV();
        people.Add(_app.Data("", Person("age", 30L)));
        people.Add(_app.Data("", Person("age", 10L)));
        people.Add(_app.Data("", Person("age", 20L)));

        var result = await people.Sort(Text("age"), Desc(false), ctx);
        await result.IsSuccess();
        var sorted = (ListV)(await result.Value())!;
        await Assert.That(((global::app.type.item.number.@this)(await (await sorted.At(0, ctx)!.Get("age")).Value())!).Clr<long>()).IsEqualTo(10L);
        await Assert.That(((global::app.type.item.number.@this)(await (await sorted.At(2, ctx)!.Get("age")).Value())!).Clr<long>()).IsEqualTo(30L);
    }

    [Test]
    public async Task SortOnListOfDict_ReturnsError()
    {
        // dict is equality-only — sorting a list of dicts (no field) is unorderable. In PLang
        // that's an EXPECTED data condition, so sort RETURNS a Data error (it does not throw —
        // a thrown exception would escape the `on error` handler pipeline).
        var ctx = Ctx();
        var dicts = new ListV();
        dicts.Add(_app.Data("", Person("city", "Reyk")));
        dicts.Add(_app.Data("", Person("city", "Oslo")));
        var result = await dicts.Sort(null, Desc(false), ctx);
        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("order");
    }

    [Test]
    public async Task UniqueUsesCompareEquality()
    {
        var ctx = Ctx();
        var values = new ListV();
        values.Add(_app.Data("", Person("city", "Reyk")));
        values.Add(_app.Data("", Person("city", "Reyk")));   // structurally equal
        values.Add(_app.Data("", Person("city", "Oslo")));
        var result = await values.Unique(ctx);
        await result.IsSuccess();
        await Assert.That((await result.Value()) as ListV).IsNotNull();
        await Assert.That(((ListV)(await result.Value())!).Count).IsEqualTo(2);
    }

    [Test]
    public async Task GroupByField_BucketsAreNavigableLists()
    {
        var ctx = Ctx();
        var people = new ListV();
        people.Add(_app.Data("", Person("city", "Reyk")));
        people.Add(_app.Data("", Person("city", "Oslo")));
        people.Add(_app.Data("", Person("city", "Reyk")));
        // each group's items as-is — the navigable bucket (list.query's group-then-order orders here instead)
        var result = await people.Group(Text("city"), b => System.Threading.Tasks.Task.FromResult(_app.Data("", b)), ctx);
        await result.IsSuccess();
        var groups = (ListV)(await result.Value())!;
        await Assert.That(groups.Count).IsEqualTo(2);
        var reyk = (DictV)(await groups.At(0, ctx)!.Value())!;
        await Assert.That((await (reyk.Get("key", ctx))!.Value())?.ToString()).IsEqualTo("Reyk");
        await Assert.That(((ListV)(await (reyk.Get("items", ctx))!.Value())!).Count).IsEqualTo(2); // navigable bucket
    }
}
