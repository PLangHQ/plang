using System.Reflection;
using app.module.action.list;
using app.variable;
using MathAdd = app.module.action.math.Add;

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
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private static async Task AssertNotDoubleWrapped(Data result, string handlerName)
    {
        // The whole point: even when T = object, .Value must be the raw payload.
        await Assert.That((await result.Value()) is Data).IsFalse()
            .Because(
                $"{handlerName} double-wrapped: result.Value is itself a Data — " +
                "the Data<object> implicit-operator footgun fired. " +
                "Use data.@this<object>.Ok(rawValue), never `return innerDataInstance;`.");
    }

    [Test]
    public async Task ListFirst_OnPopulatedList_ValueIsRawNotData()
    {
        var context = _app.User.Context;
        context.Variable.Set("xs", new List<object?> { 42L, "two", "three" });

        var action = new First(context) { ListName = new @this("xs") };
        await action.Attach(null, context);
        var result = await action.Run();

        await result.IsSuccess();
        await AssertNotDoubleWrapped(result, "list.first");
        // Sanity — value is the raw 42L, not Data{42L}.
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task ListGet_OnPopulatedList_ValueIsRawNotData()
    {
        var context = _app.User.Context;
        context.Variable.Set("xs", new List<object?> { "a", "b", "c" });

        var action = new Get(context) { ListName = new @this("xs"), Index = (global::app.type.item.number.@this)1 };
        await action.Attach(null, context);
        var result = await action.Run();

        await result.IsSuccess();
        await AssertNotDoubleWrapped(result, "list.get");
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("b");
    }

    [Test]
    public async Task ListLast_OnPopulatedList_ValueIsRawNotData()
    {
        var context = _app.User.Context;
        context.Variable.Set("xs", new List<object?> { 1L, 2L, 3L });

        var action = new Last(context) { ListName = new @this("xs") };
        await action.Attach(null, context);
        var result = await action.Run();

        await result.IsSuccess();
        await AssertNotDoubleWrapped(result, "list.last");
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("3");
    }

    [Test]
    public async Task MathAdd_OnLongs_ValueIsRawNotData()
    {
        var context = _app.User.Context;
        var action = new MathAdd(context) { A = new Data("", 5L, context: context), B = new Data("", 3L, context: context) };
        await action.Attach(null, context);
        var result = await action.Run();

        await result.IsSuccess();
        await AssertNotDoubleWrapped(result, "math.add");
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("8");
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
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace?.StartsWith("app.module") == true)
            .Select(t => (Type: t, Run: t.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes)))
            .Where(x => x.Run != null && IsTaskDataOfObject(x.Run.ReturnType))
            .Select(x => x.Type.FullName!)
            .OrderBy(n => n)
            .ToList();

        // The scalars-as-native cascade removed every `Task<Data<object>>` Run handler:
        // known-type returns took a concrete wrapper (math.Random → Data<number>), and
        // genuinely-polymorphic ones (list.First/Get/Last/Where, signing.sign, llm.query)
        // took bare Task<Data> (no T → the `where T : item` constraint can't be violated).
        // No handler may reintroduce Data<object> — polymorphic = bare Data, known =
        // Data<concreteWrapper>.
        var expected = System.Array.Empty<string>();
        await Assert.That(dataObjectHandlers).IsEquivalentTo(expected)
            .Because(
                "New Data<object> Run() handler? Either narrow T to a concrete type, " +
                "or add a runtime double-wrap invocation test in this file. " +
                "Forwarders that polymorphically return a Data produced elsewhere should be Task<Data>, not Task<Data<object>>.");
    }

    // Structural probe for `Task<Data<object>>`. The closed type can no longer be named in
    // source (`where T : item` rejects `object`), so we inspect already-compiled return types
    // through the open generics instead of comparing against a `typeof(...)`.
    private static bool IsTaskDataOfObject(System.Type returnType)
    {
        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
            return false;
        var inner = returnType.GetGenericArguments()[0];
        return inner.IsGenericType
            && inner.GetGenericTypeDefinition() == typeof(global::app.data.@this<>)
            && inner.GetGenericArguments()[0] == typeof(object);
    }

    [Test]
    public async Task ListWhere_ResultValueIsListNotData()
    {
        // where wraps a list/dict value (owned construction), never an inner Data —
        // so Data<object>.Ok does not double-wrap.
        var app = TestApp.Create("/app");
        var context = app.User.Context;
        var users = new global::app.type.item.list.@this(context);
        var u1 = new global::app.type.item.dict.@this(context); u1.Set(new global::app.data.@this("age", 25L, context: context)); users.Add(new global::app.data.@this("", u1));
        var u2 = new global::app.type.item.dict.@this(context); u2.Set(new global::app.data.@this("age", 15L, context: context)); users.Add(new global::app.data.@this("", u2));
        context.Variable.Set("users", users);

        var action = new global::app.module.action.list.Where(context) { ListName = new @this("users"),
            Field = new global::app.data.@this<global::app.type.item.text.@this>("", "age"),
            Operator = new global::app.data.@this<global::app.type.item.choice.@this<global::app.module.action.condition.Operator>>("", new global::app.module.action.condition.Operator(">")),
            Value = new global::app.data.@this("", 20L, context: context),
        };
        await action.Attach(null, context);
        var result = await action.Run();
        await result.IsSuccess();
        await Assert.That((await result.Value())).IsTypeOf<global::app.type.item.list.@this>();
        await Assert.That(((global::app.type.item.list.@this)(await result.Value())!).Count).IsEqualTo(1);
        await app.DisposeAsync();
    }
}
