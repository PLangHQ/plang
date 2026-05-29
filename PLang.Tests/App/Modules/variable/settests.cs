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
        _app = new global::app.@this("/app");
    }

    [Test]
    public async Task Set_SetsVariable()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%testVar%"), ("value", "testValue"));
        var result = await action.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.GetValue("testVar")).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_WithType_SetsTypeInfo()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%count%"), ("value", 42), ("type", "int"));
        var result = await action.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.Get("count")!.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Set_ReturnsOk()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%testVar%"), ("value", "testValue"));
        var result = await action.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.GetValue("testVar")).IsEqualTo("testValue");
        // F3-1: handler must return the stored value, not an empty Data.Ok().
        // Powers %!data% capture in goal.call → ReturnMapping / GoalCallReturn PLang tests.
        await Assert.That(result.Value).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_WithType_SetsTypeOnStoredVariable()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%count%"), ("value", 42), ("type", "int"));
        var result = await action.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.Get("count")!.Type!.Value).IsEqualTo("int");
    }

    [Test]
    public async Task Set_AsDefault_DoesNotOverwriteExisting()
    {
        var context = _app.User.Context;

        // Set initial value
        var setAction = TestAction.Create("variable", "set", ("name", "%x%"), ("value", "original"));
        await setAction.RunAsync(context);

        // Try to set default — should not overwrite
        var defaultAction = TestAction.Create("variable", "set", ("name", "%x%"), ("value", "default"), ("asdefault", true));
        var result = await defaultAction.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.GetValue("x")).IsEqualTo("original");
        // F3-1: when AsDefault hits an existing var, handler returns the existing Data,
        // not an empty Data.Ok(). Reverting that branch would surface here.
        await Assert.That(result.Value).IsEqualTo("original");
    }

    [Test]
    public async Task Set_AsDefault_SetsWhenNotExists()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%y%"), ("value", "default"), ("asdefault", true));
        var result = await action.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.GetValue("y")).IsEqualTo("default");
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

        var result = await action.RunAsync(context);

        await Assert.That(result.Success).IsTrue();

        var dataVar = context.Variables.Get("!data");
        var myVar = context.Variables.Get("myVar");

        // Aliasing: same Data reachable under both keys.
        await Assert.That(ReferenceEquals(dataVar, result)).IsTrue();
        await Assert.That(ReferenceEquals(myVar, result)).IsTrue();

        // No rename: the `value` parameter Data flows through unchanged — Name
        // stays "value" (its parameter name). If RunAsync mutated to "!data",
        // this fires.
        await Assert.That(result.Name).IsNotEqualTo("!data");
    }

    // --- ValidateBuild tests ---

    [Test]
    public async Task ValidateBuild_LiteralThis_ReturnsError()
    {
        var parameters = new List<Data> { new Data("Value", "this") };
        var result = global::app.module.variable.Set.ValidateBuild(parameters);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!).Contains("this");
    }

    [Test]
    public async Task ValidateBuild_VariableReference_ReturnsNull()
    {
        var parameters = new List<Data>
        {
            new Data("Value", "%myVar%", global::app.type.@this.FromName("int"))
        };
        var result = global::app.module.variable.Set.ValidateBuild(parameters);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ValidateBuild_TypeMismatch_ReturnsError()
    {
        var parameters = new List<Data>
        {
            new Data("Value", "not a number", global::app.type.@this.FromName("int"))
        };
        var result = global::app.module.variable.Set.ValidateBuild(parameters);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!).Contains("type=int");
    }

    [Test]
    public async Task ValidateBuild_ValidTypeMatch_ReturnsNull()
    {
        var parameters = new List<Data>
        {
            new Data("Value", 42, global::app.type.@this.FromName("int"))
        };
        var result = global::app.module.variable.Set.ValidateBuild(parameters);

        await Assert.That(result).IsNull();
    }
}
