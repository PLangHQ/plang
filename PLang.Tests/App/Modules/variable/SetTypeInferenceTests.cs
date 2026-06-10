using app.actor.context;
using app;
using app.variable;

namespace PLang.Tests.App.actions.variable;

// variable.set binding contract:
//   - No `as` clause → shallow-clone the source Data under the target name (plain
//     Data; no Data<T> mint, no materialize, no deep clone). The PLang type is what's
//     inferred — it derives from the value's CLR type and rides on .Type. Containers
//     are structurally copied by the param-resolution walk (new list/dict, shared
//     leaves), so the stored value is a distinct instance.
//   - `as` clause → convert and mint the declared Data<T>.
//   - null → plain Data (un-typed).

public class SetTypeInferenceTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    [Test]
    public async Task Set_StringValue_InfersTextType()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%s%"), ("value", "hello"));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("s");
        await Assert.That((await stored.Value())?.ToString()).IsEqualTo("hello");
        await Assert.That(stored.Type.Name).IsEqualTo("text");
    }

    [Test]
    public async Task Set_IntValue_InfersNumberType()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%n%"), ("value", 42));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("n");
        // The .pr/JSON pipeline normalizes integers to long; .Type derives "number".
        await Assert.That(System.Convert.ToInt64((await stored.Value()))).IsEqualTo(42L);
        await Assert.That(stored.Type.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Set_LongValue_InfersNumberType()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%n%"), ("value", 42L));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("n");
        await Assert.That((await stored.Value())?.ToString()).IsEqualTo("42");
        await Assert.That(stored.Type.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Set_DoubleValue_InfersNumberType()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%d%"), ("value", 3.14));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("d");
        await Assert.That((await stored.Value())?.ToString()).IsEqualTo("3.14");
        await Assert.That(stored.Type.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Set_BoolValue_InfersBoolType()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%b%"), ("value", true));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("b");
        await Assert.That((await stored.Value())).IsEqualTo(true);
        await Assert.That(stored.Type.Name).IsEqualTo("bool");
    }

    [Test]
    public async Task Set_DateTimeValue_InfersDateTimeType()
    {
        var context = _app.User.Context;
        var when = DateTime.UtcNow;
        var action = TestAction.Create("variable", "set", ("name", "%t%"), ("value", when));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("t");
        await Assert.That((await stored.Value())).IsEqualTo(when);
        await Assert.That(stored.Type.Name).IsEqualTo("datetime");
    }

    [Test]
    public async Task Set_ListValue_StoresDistinctListInstance()
    {
        var context = _app.User.Context;
        var src = new List<object?> { "a", "b" };
        var action = TestAction.Create("variable", "set", ("name", "%list%"), ("value", src));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("list");
        await Assert.That((await stored.Value())).IsTypeOf<List<object?>>();
        // The param-resolution walk copies the container — stored.Value is a distinct list.
        await Assert.That(ReferenceEquals((stored.Materialize()), src)).IsFalse();
    }

    [Test]
    public async Task Set_DictValue_StoresDistinctDictInstance()
    {
        var context = _app.User.Context;
        var src = new Dictionary<string, object?> { ["k"] = "v" };
        var action = TestAction.Create("variable", "set", ("name", "%d%"), ("value", src));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("d");
        await Assert.That((await stored.Value())).IsTypeOf<Dictionary<string, object?>>();
        await Assert.That(ReferenceEquals((stored.Materialize()), src)).IsFalse();
    }

    [Test]
    public async Task Set_ForcedType_String_ConvertsAndMintsDataOfString()
    {
        var context = _app.User.Context;
        // Source value is int 42; forced Type="string" should produce Data<global::app.type.text.@this> "42".
        var action = TestAction.Create("variable", "set", ("name", "%n%"), ("value", 42), ("type", "string"));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("n");
        await Assert.That(stored).IsTypeOf<global::app.data.@this<global::app.type.text.@this>>();
        await Assert.That((await stored.Value())!.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task Set_ForcedType_ConversionFailure_ReturnsError()
    {
        var context = _app.User.Context;
        // "abc" can't convert to int → handler returns Data with Error.
        var action = TestAction.Create("variable", "set", ("name", "%n%"), ("value", "abc"), ("type", "int"));
        var result = await action.RunAsync(context);
        await result.IsFailure();
    }

    [Test]
    public async Task Set_NullValue_MintsPlainDataNotGeneric()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%x%"), ("value", null));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("x");
        // Plain Data (not Data<T>) — null can't be type-inferred.
        await Assert.That(stored.GetType()).IsEqualTo(typeof(global::app.data.@this));
    }

    [Test]
    public async Task Set_AsDefault_ExistingInitialized_DoesNotReplace()
    {
        var context = _app.User.Context;
        await TestAction.Create("variable", "set", ("name", "%x%"), ("value", "first")).RunAsync(context);
        var result = await TestAction.Create("variable", "set", ("name", "%x%"), ("value", "second"), ("asdefault", true)).RunAsync(context);
        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("x"))).IsEqualTo("first");
    }
}
