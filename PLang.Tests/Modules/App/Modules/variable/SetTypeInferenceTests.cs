using app.actor.context;
using app;
using app.variable;

namespace PLang.Tests.App.actions.variable;

// variable.set binding contract:
//   - No `as` clause → shallow-clone the source Data under the target name (plain
//     Data; no Data<T> mint, no materialize, no deep clone). The PLang type is what's
//     inferred — it derives from the value's CLR type and rides on .Type. Collections
//     are reference semantics: the stored value is the SAME instance (nothing is
//     copied); only the property bag is fresh per binding.
//   - `as` clause → convert and mint the declared Data<T>.
//   - null → plain Data (un-typed).

public class SetTypeInferenceTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

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
        await Assert.That((await stored.Value())?.ToString()).IsEqualTo("true");
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
        await Assert.That(global::app.type.item.@this.Lower<System.DateTimeOffset>(await stored.Value())).IsEqualTo(when);
        await Assert.That(stored.Type.Name).IsEqualTo("datetime");
    }

    // A CLR list is aliased as the new list's backing — no JSON walk, no copy. The
    // value door answers the native list.@this; the CLR exit door hands the SAME
    // List<object?> instance back as long as the list was only read (every slot
    // still raw).
    [Test]
    public async Task Set_ListValue_AliasesSourceInstance()
    {
        var context = _app.User.Context;
        var src = new List<object?> { "a", "b" };
        var action = TestAction.Create("variable", "set", ("name", "%list%"), ("value", src));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("list");
        var lst = (await stored.Value()) as global::app.type.list.@this;
        await Assert.That(lst).IsNotNull();
        // The CLR exit door returns the aliased backing itself — same ref, O(1).
        await Assert.That(ReferenceEquals(lst!.Clr<List<object?>>(), src)).IsTrue();
    }

    // No copy at set: a later in-place edit of the SOURCE list is visible through
    // the plang variable (proof the backing was aliased, never walked/copied).
    [Test]
    public async Task Set_ListValue_AliasNotCopied_SourceEditVisible()
    {
        var context = _app.User.Context;
        var src = new List<object?> { "a", "b" };
        await (await TestAction.Create("variable", "set", ("name", "%list%"), ("value", src)).RunAsync(context)).IsSuccess();
        src.Add("c");
        var lst = (await (await context.Variable.Get("list")).Value()) as global::app.type.list.@this;
        await Assert.That(lst!.CountRaw).IsEqualTo(3);
    }

    // A write elevates a slot to a Data — the backing diverges from its pristine
    // all-raw aliased form, so the CLR exit door peels (a NEW list, no longer the
    // source ref): "modify the list → not the same ref." The written row reads back
    // narrowed to its type.
    [Test]
    public async Task Set_ListIndexWrite_ElevatesSlot_ClrRebuilds()
    {
        var context = _app.User.Context;
        var src = new List<object?> { "a", "b", "c" };
        var lst = new global::app.type.list.@this(src) { Context = context };

        lst.SetAt(2, new Data("", 9L, context: context));

        await Assert.That(ReferenceEquals(lst.Clr<List<object?>>(), src)).IsFalse();
        await Assert.That((await lst.At(2)!.Value())).IsTypeOf<global::app.type.number.@this>();
    }

    // A Dictionary<string,object?> is aliased the same way — the CLR exit door
    // returns the same instance after a pure read.
    [Test]
    public async Task Set_DictValue_AliasesSourceInstance()
    {
        var context = _app.User.Context;
        var src = new Dictionary<string, object?> { ["k"] = "v" };
        var action = TestAction.Create("variable", "set", ("name", "%d%"), ("value", src));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("d");
        var d = (await stored.Value()) as global::app.type.dict.@this;
        await Assert.That(d).IsNotNull();
        // A read of a key must not knock the dict off the same-ref fast path.
        await Assert.That((await d!.Get("k")!.Value())?.ToString()).IsEqualTo("v");
        await Assert.That(ReferenceEquals(d.Clr<Dictionary<string, object?>>(), src)).IsTrue();
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
        // Forced `as string` converted 42 → text "42"; the stored value IS a text (born-typed).
        await Assert.That(await stored.Value()).IsTypeOf<global::app.type.text.@this>();
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

    // The [1,2,3] rule: set %y% = %x% shares the list INSTANCE — an in-place
    // add through one name is visible through the other, like List<T> in C#.
    [Test]
    public async Task Set_ListAlias_InPlaceAddVisibleThroughBothNames()
    {
        var context = _app.User.Context;
        var x = new global::app.type.list.@this { Context = context };
        x.Add(new Data("", 1L, context: context)); x.Add(new Data("", 2L, context: context));
        context.Variable.Set("x", x);

        var alias = TestAction.Create("variable", "set", ("name", "%y%"), ("value", "%x%"));
        await (await alias.RunAsync(context)).IsSuccess();

        var add = TestAction.Create("list", "add", ("listname", "%x%"), ("value", 3));
        await (await add.RunAsync(context)).IsSuccess();

        var y = await context.Variable.Get("y");
        var yList = (await y.Value()) as global::app.type.list.@this;
        await Assert.That(yList).IsNotNull();
        await Assert.That(yList!.CountRaw).IsEqualTo(3);
        await Assert.That((await yList.At(2)!.Value())?.ToString()).IsEqualTo("3");
    }

    // The binding owns its property bag: a property write on the alias lands
    // on the alias only (the bag is copied at set; values inside it are
    // shared by pointer).
    [Test]
    public async Task Set_Alias_PropertyWrite_LandsOnAliasOnly()
    {
        var context = _app.User.Context;
        context.Variable.Set("x", "payload");

        var alias = TestAction.Create("variable", "set", ("name", "%y%"), ("value", "%x%"));
        await (await alias.RunAsync(context)).IsSuccess();

        var y = await context.Variable.Get("y");
        y.Properties["NewProp"] = 1L;

        var x = await context.Variable.Get("x");
        await Assert.That(x.Properties.ContainsKey("NewProp")).IsFalse();
        await Assert.That(y.Properties.ContainsKey("NewProp")).IsTrue();
    }
}
