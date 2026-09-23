using app.actor.context;
using app;
using app.variable;

namespace PLang.Tests.App.actions.variable;

public class SetTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/app");
    }

    [Test]
    public async Task Set_SetsVariable()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%testVar%"), ("value", "testValue"));
        var result = await action.Run(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("testVar"))).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_BangPath_WritesSetting_NotVariable()
    {
        // `set %!http.request.timeout% = 5` lands on context.Setting (where the generator seam
        // reads it) — the write side of the setting front door — not on the variable store.
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%!http.request.timeout%"), ("value", 5));
        var result = await action.Run(context);

        await result.IsSuccess();
        await Assert.That((await context.Setting.Get(global::app.setting.Storage.InMemory, "http.request.timeout")).Success).IsTrue();
        await Assert.That((await context.Variable.GetValue("!http.request.timeout"))).IsNull();
    }

    [Test]
    public async Task Set_WithType_SetsTypeInfo()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%count%"), ("value", 42), ("type", "int"));
        var result = await action.Run(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.Get("count"))!.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Set_ReturnsOk()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%testVar%"), ("value", "testValue"));
        var result = await action.Run(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("testVar"))).IsEqualTo("testValue");
        // F3-1: handler must return the stored value, not an empty Data.Ok().
        // Powers %!data% capture in goal.call → ReturnMapping / GoalCallReturn PLang tests.
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_WithType_SetsTypeOnStoredVariable()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%count%"), ("value", 42), ("type", "int"));
        var result = await action.Run(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.Get("count"))!.Type!.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Set_AsDefault_DoesNotOverwriteExisting()
    {
        var context = _app.User.Context;

        // Set initial value
        var setAction = TestAction.Create("variable", "set", ("name", "%x%"), ("value", "original"));
        await setAction.Run(context);

        // Try to set default — should not overwrite
        var defaultAction = TestAction.Create("variable", "set", ("name", "%x%"), ("value", "default"), ("asdefault", true));
        var result = await defaultAction.Run(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("x"))).IsEqualTo("original");
        // F3-1: when AsDefault hits an existing var, handler returns the existing Data,
        // not an empty Data.Ok(). Reverting that branch would surface here.
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("original");
    }

    [Test]
    public async Task Set_AsDefault_SetsWhenNotExists()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%y%"), ("value", "default"), ("asdefault", true));
        var result = await action.Run(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("y"))).IsEqualTo("default");
    }

    [Test]
    public async Task ActionRunAsync_AliasesResultUnderData_DoesNotMutateName()
    {
        // F3-2: Action.RunAsync must alias the handler's result under %!data%
        // WITHOUT mutating result.Name. Old code did `result.Name = "!data";
        // Variables.Put(result);` — that corrupted any producer Data shared between
        // %!data% and its source variable entry.
        //
        // Invariant: after RunAsync, %!data% and the handler's own stored entry
        // must be the SAME reference, and the Data's Name must be whatever the
        // handler set it to — never overwritten to "!data".
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%myVar%"), ("value", "hello"));

        var result = await action.Run(context);

        await result.IsSuccess();

        var dataVar = await context.Variable.Get("!data");
        var myVar = await context.Variable.Get("myVar");

        // Aliasing: same Data reachable under both keys.
        await Assert.That(ReferenceEquals(dataVar, result)).IsTrue();
        await Assert.That(ReferenceEquals(myVar, result)).IsTrue();

        // No rename: the `value` parameter Data flows through unchanged — Name
        // stays "value" (its parameter name). If RunAsync mutated to "!data",
        // this fires.
        await Assert.That(result.Name).IsNotEqualTo("!data");
    }

    // --- Validate tests (the bound handler judging its own properties) ---

    private global::app.module.action.variable.Set WithValue(object value, string type)
        => new(_app.User.Context)
        {
            Value = new Data("Value", value, global::PLang.Tests.TestApp.SharedContext.Type.Create(type), context: _app.User.Context)
        };

    [Test]
    public async Task Validate_VariableReference_ReturnsNull()
    {
        var result = await WithValue("%myVar%", "int").Validate();

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Validate_TypeMismatch_ReturnsError()
    {
        var result = await WithValue("not a number", "int").Validate();

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Message).Contains("type=number");
    }

    [Test]
    public async Task Validate_ValidTypeMatch_ReturnsNull()
    {
        var result = await WithValue(42, "int").Validate();

        await Assert.That(result).IsNull();
    }
}
