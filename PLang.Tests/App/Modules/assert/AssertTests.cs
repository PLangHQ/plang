using App.Context;
using App;
using App.Errors;
using App.Variables;
using AssertEquals = App.modules.assert.Equals;
using AssertNotEquals = App.modules.assert.NotEquals;
using AssertIsTrue = App.modules.assert.IsTrue;
using AssertIsFalse = App.modules.assert.IsFalse;
using AssertIsNull = App.modules.assert.IsNull;
using AssertIsNotNull = App.modules.assert.IsNotNull;
using AssertContains = App.modules.assert.Contains;
using AssertGreaterThan = App.modules.assert.GreaterThan;
using AssertLessThan = App.modules.assert.LessThan;

namespace PLang.Tests.App.actions.assert;

public class AssertTests
{
    private (Context.@this context, Variables memory) CreateContext()
    {
        var engine = new App.@this("/app");
        var memory = new Variables();
        var context = new Context.@this(engine, memory);
        return (context, memory);
    }

    private static Data D(object? value) => value == null ? new Data("") : Data.Ok(value);

    // --- Equals ---

    [Test]
    public async Task Equals_SameInts_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D(42), Actual = D(42) };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Equals_DifferentValues_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D(42), Actual = D(99) };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error is AssertionError).IsTrue();
    }

    [Test]
    public async Task Equals_IntAndDouble_CoercesAndPasses()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D(5), Actual = D(5.0) };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Equals_Strings_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D("hello"), Actual = D("hello") };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Equals_NullBothSides_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D(null), Actual = D(null) };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Equals_NullVsValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D(null), Actual = D(5) };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Equals_CustomMessage_IncludedInError()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D(1), Actual = D(2), Message = "Sum check" };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
        var error = result.Error as AssertionError;
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.UserMessage).IsEqualTo("Sum check");
    }

    // --- NotEquals ---

    [Test]
    public async Task NotEquals_DifferentValues_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertNotEquals { Context = context, Expected = D(1), Actual = D(2) };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task NotEquals_SameValues_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertNotEquals { Context = context, Expected = D(5), Actual = D(5) };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    // --- IsTrue ---

    [Test]
    public async Task IsTrue_TrueValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = D(true) };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task IsTrue_FalseValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = D(false) };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task IsTrue_NonZeroNumber_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = D(42) };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task IsTrue_Zero_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = D(0) };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task IsTrue_Null_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = D(null) };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    // --- IsFalse ---

    [Test]
    public async Task IsFalse_FalseValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsFalse { Context = context, Value = D(false) };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task IsFalse_TrueValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsFalse { Context = context, Value = D(true) };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task IsFalse_Null_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsFalse { Context = context, Value = D(null) };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    // --- IsNull ---

    [Test]
    public async Task IsNull_NullValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsNull { Context = context, Value = D(null) };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task IsNull_NonNullValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsNull { Context = context, Value = D("hello") };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    // --- IsNotNull ---

    [Test]
    public async Task IsNotNull_NonNullValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsNotNull { Context = context, Value = D("hello") };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task IsNotNull_NullValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsNotNull { Context = context, Value = D(null) };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    // --- Contains ---

    [Test]
    public async Task Contains_StringContainsSubstring_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertContains { Context = context, Value = D("hello world"), Container = D("world") };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Contains_StringDoesNotContain_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertContains { Context = context, Value = D("hello world"), Container = D("xyz") };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Contains_ListContainsElement_Passes()
    {
        var (context, _) = CreateContext();
        var list = new List<object> { 1, 2, 3 };
        var action = new AssertContains { Context = context, Value = D(list), Container = D(2) };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Contains_ListDoesNotContain_Fails()
    {
        var (context, _) = CreateContext();
        var list = new List<object> { 1, 2, 3 };
        var action = new AssertContains { Context = context, Value = D(list), Container = D(99) };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Contains_NullValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertContains { Context = context, Value = D(null), Container = D("x") };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    // --- GreaterThan ---

    [Test]
    public async Task GreaterThan_LargerValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertGreaterThan { Context = context, A = D(10), B = D(5) };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task GreaterThan_EqualValues_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertGreaterThan { Context = context, A = D(5), B = D(5) };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task GreaterThan_SmallerValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertGreaterThan { Context = context, A = D(3), B = D(5) };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    // --- LessThan ---

    [Test]
    public async Task LessThan_SmallerValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertLessThan { Context = context, A = D(3), B = D(5) };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task LessThan_EqualValues_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertLessThan { Context = context, A = D(5), B = D(5) };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task LessThan_LargerValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertLessThan { Context = context, A = D(10), B = D(5) };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }
}
