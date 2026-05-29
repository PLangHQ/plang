using System.Reflection;
using app.modules.list;
using app.variables;
using MathAdd = app.modules.math.Add;

namespace PLang.Tests.App.TypedReturnsTests;

// Runtime footgun guard: the implicit `data.@this<T>(T value)` operator
// silently double-wraps when T = object and the source is already a Data —
// producing Data<object>{ Value = Data<X>{...} }. The static checks in
// Stage2_MechanicalTypings only verify T at the type level; this file
// invokes typed Run() handlers and asserts result.Value is the raw payload,
// not a nested Data instance.
public class RuntimeDoubleWrapTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private static async Task AssertNotDoubleWrapped(Data result, string handlerName)
    {
        // The whole point: even when T = object, .Value must be the raw payload.
        await Assert.That(result.Value is Data).IsFalse()
            .Because(
                $"{handlerName} double-wrapped: result.Value is itself a Data — " +
                "the Data<object> implicit-operator footgun fired. " +
                "Use data.@this<object>.Ok(rawValue), never `return innerDataInstance;`.");
    }

    [Test]
    public async Task ListFirst_OnPopulatedList_ValueIsRawNotData()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set("xs", new List<object?> { 42L, "two", "three" });

        var action = new First { Context = ctx, ListName = new Variable("xs") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await AssertNotDoubleWrapped(result, "list.first");
        // Sanity — value is the raw 42L, not Data{42L}.
        await Assert.That(result.Value).IsEqualTo(42L);
    }

    [Test]
    public async Task ListGet_OnPopulatedList_ValueIsRawNotData()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set("xs", new List<object?> { "a", "b", "c" });

        var action = new Get { Context = ctx, ListName = new Variable("xs"), Index = 1 };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await AssertNotDoubleWrapped(result, "list.get");
        await Assert.That(result.Value).IsEqualTo("b");
    }

    [Test]
    public async Task ListLast_OnPopulatedList_ValueIsRawNotData()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set("xs", new List<object?> { 1L, 2L, 3L });

        var action = new Last { Context = ctx, ListName = new Variable("xs") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await AssertNotDoubleWrapped(result, "list.last");
        await Assert.That(result.Value).IsEqualTo(3L);
    }

    [Test]
    public async Task MathAdd_OnLongs_ValueIsRawNotData()
    {
        var ctx = _app.User.Context;
        var action = new MathAdd { Context = ctx, A = new Data("", 5L), B = new Data("", 3L) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await AssertNotDoubleWrapped(result, "math.add");
        await Assert.That(result.Value).IsEqualTo(8L);
    }

    // Sweep across every action handler whose Run() returns Task<Data<object>>:
    // the implicit-operator footgun only bites when T = object. The list is a
    // tripwire — when it grows, the reviewer audits the new handler's
    // construction path. New Data<object> Run() ⇒ either narrow T to a
    // concrete type, or add a runtime invocation test below.
    [Test]
    public async Task EveryDataObjectRunHandler_IsKnownToThisTest()
    {
        var dataObjectHandlers = typeof(global::app.@this).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace?.StartsWith("app.modules") == true)
            .Select(t => (Type: t, Run: t.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes)))
            .Where(x => x.Run != null && x.Run.ReturnType == typeof(Task<global::app.data.@this<object>>))
            .Select(x => x.Type.FullName!)
            .OrderBy(n => n)
            .ToList();

        // Note: math.Add/Subtract/Multiply/Divide/Modulo/Power/IntDiv migrated
        // to Task<Data<number>> in plang-types Stage 4; abs/ceiling/floor/sqrt/
        // round/min/max followed on the MathHelper-deletion branch. math.Random
        // is still Data<object> until it gets its own number retype.
        var expected = new[]
        {
            "app.modules.list.First",
            "app.modules.list.Get",
            "app.modules.list.Last",
            "app.modules.math.Random",
            "app.modules.signing.sign",
        };
        await Assert.That(dataObjectHandlers).IsEquivalentTo(expected)
            .Because(
                "New Data<object> Run() handler? Either narrow T to a concrete type, " +
                "or add a runtime double-wrap invocation test in this file. " +
                "Forwarders that polymorphically return a Data produced elsewhere should be Task<Data>, not Task<Data<object>>.");
    }
}
