using global::App.Actor.Context;
using App;
using global::App.Variables;

namespace PLang.Tests.App.actions.variable;

// Phase 4 contract — variable.set is the SOLE binding-mint site. With Phase 3's
// Variables.Set reduced to dumb storage (Set(Data dv) only), variable.set has
// to construct the Data itself. Architect/v1 §Phase 4:
//   1. If [Type] parameter set → construct Data<T> via App.Data.Type.FromName.
//   2. Else if-chain on Value.Value's runtime type.
//   3. Mutable refs (List, Dict) snapshot-cloned via JSON roundtrip.
//   4. null → plain Data (un-typed).
//   5. Anything else → reflection fallback.

public class SetTypeInferenceTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::App.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    [Test]
    public async Task Set_StringValue_MintsDataOfString()
    {
        var ctx = _app.Context;
        var action = TestAction.Create("variable", "set", ("name", "%s%"), ("value", "hello"));
        var result = await _app.Run(action, ctx);
        await Assert.That(result.Success).IsTrue();
        var stored = ctx.Variables.Get("s");
        await Assert.That(stored).IsTypeOf<global::App.Data.@this<string>>();
    }

    [Test]
    public async Task Set_IntValue_MintsDataOfInt()
    {
        var ctx = _app.Context;
        var action = TestAction.Create("variable", "set", ("name", "%n%"), ("value", 42));
        var result = await _app.Run(action, ctx);
        await Assert.That(result.Success).IsTrue();
        var stored = ctx.Variables.Get("n");
        // The .pr/JSON pipeline normalizes integers to long; that's consistent with the
        // architect's hot-types if-chain (long arm fires for the LLM-emitted form).
        var t = stored.GetType();
        var isInt = t == typeof(global::App.Data.@this<int>);
        var isLong = t == typeof(global::App.Data.@this<long>);
        await Assert.That(isInt || isLong).IsTrue();
    }

    [Test]
    public async Task Set_LongValue_MintsDataOfLong()
    {
        var ctx = _app.Context;
        var action = TestAction.Create("variable", "set", ("name", "%n%"), ("value", 42L));
        var result = await _app.Run(action, ctx);
        await Assert.That(result.Success).IsTrue();
        var stored = ctx.Variables.Get("n");
        await Assert.That(stored).IsTypeOf<global::App.Data.@this<long>>();
    }

    [Test]
    public async Task Set_DoubleValue_MintsDataOfDouble()
    {
        var ctx = _app.Context;
        var action = TestAction.Create("variable", "set", ("name", "%d%"), ("value", 3.14));
        var result = await _app.Run(action, ctx);
        await Assert.That(result.Success).IsTrue();
        var stored = ctx.Variables.Get("d");
        await Assert.That(stored).IsTypeOf<global::App.Data.@this<double>>();
    }

    [Test]
    public async Task Set_BoolValue_MintsDataOfBool()
    {
        var ctx = _app.Context;
        var action = TestAction.Create("variable", "set", ("name", "%b%"), ("value", true));
        var result = await _app.Run(action, ctx);
        await Assert.That(result.Success).IsTrue();
        var stored = ctx.Variables.Get("b");
        await Assert.That(stored).IsTypeOf<global::App.Data.@this<bool>>();
    }

    [Test]
    public async Task Set_DateTimeValue_MintsDataOfDateTime()
    {
        var ctx = _app.Context;
        var when = DateTime.UtcNow;
        var action = TestAction.Create("variable", "set", ("name", "%t%"), ("value", when));
        var result = await _app.Run(action, ctx);
        await Assert.That(result.Success).IsTrue();
        var stored = ctx.Variables.Get("t");
        await Assert.That(stored).IsTypeOf<global::App.Data.@this<DateTime>>();
    }

    [Test]
    public async Task Set_ListValue_MintsDataOfListAndSnapshotClones()
    {
        var ctx = _app.Context;
        var src = new List<object?> { "a", "b" };
        var action = TestAction.Create("variable", "set", ("name", "%list%"), ("value", src));
        var result = await _app.Run(action, ctx);
        await Assert.That(result.Success).IsTrue();
        var stored = ctx.Variables.Get("list");
        await Assert.That(stored).IsTypeOf<global::App.Data.@this<List<object?>>>();
        // Snapshot-clone: stored.Value is a separate list instance.
        await Assert.That(ReferenceEquals(stored.Value, src)).IsFalse();
    }

    [Test]
    public async Task Set_DictValue_MintsDataOfDictionary()
    {
        var ctx = _app.Context;
        var src = new Dictionary<string, object?> { ["k"] = "v" };
        var action = TestAction.Create("variable", "set", ("name", "%d%"), ("value", src));
        var result = await _app.Run(action, ctx);
        await Assert.That(result.Success).IsTrue();
        var stored = ctx.Variables.Get("d");
        await Assert.That(stored).IsTypeOf<global::App.Data.@this<Dictionary<string, object?>>>();
        await Assert.That(ReferenceEquals(stored.Value, src)).IsFalse();
    }

    [Test]
    public async Task Set_ForcedType_String_ConvertsAndMintsDataOfString()
    {
        var ctx = _app.Context;
        // Source value is int 42; forced Type="string" should produce Data<string> "42".
        var action = TestAction.Create("variable", "set", ("name", "%n%"), ("value", 42), ("type", "string"));
        var result = await _app.Run(action, ctx);
        await Assert.That(result.Success).IsTrue();
        var stored = ctx.Variables.Get("n");
        await Assert.That(stored).IsTypeOf<global::App.Data.@this<string>>();
        await Assert.That(stored.Value).IsEqualTo("42");
    }

    [Test]
    public async Task Set_ForcedType_ConversionFailure_ReturnsError()
    {
        var ctx = _app.Context;
        // "abc" can't convert to int → handler returns Data with Error.
        var action = TestAction.Create("variable", "set", ("name", "%n%"), ("value", "abc"), ("type", "int"));
        var result = await _app.Run(action, ctx);
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Set_NullValue_MintsPlainDataNotGeneric()
    {
        var ctx = _app.Context;
        var action = TestAction.Create("variable", "set", ("name", "%x%"), ("value", null));
        var result = await _app.Run(action, ctx);
        await Assert.That(result.Success).IsTrue();
        var stored = ctx.Variables.Get("x");
        // Plain Data (not Data<T>) — null can't be type-inferred.
        await Assert.That(stored.GetType()).IsEqualTo(typeof(global::App.Data.@this));
    }

    [Test]
    public async Task Set_AsDefault_ExistingInitialized_DoesNotReplace()
    {
        var ctx = _app.Context;
        await _app.Run(TestAction.Create("variable", "set", ("name", "%x%"), ("value", "first")), ctx);
        var result = await _app.Run(TestAction.Create("variable", "set", ("name", "%x%"), ("value", "second"), ("asdefault", true)), ctx);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(ctx.Variables.GetValue("x")).IsEqualTo("first");
    }
}
