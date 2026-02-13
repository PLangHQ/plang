using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;
using AssertEquals = PLang.Runtime2.modules.assert.Equals;
using AssertNotEquals = PLang.Runtime2.modules.assert.NotEquals;
using AssertIsTrue = PLang.Runtime2.modules.assert.IsTrue;
using AssertIsFalse = PLang.Runtime2.modules.assert.IsFalse;
using AssertIsNull = PLang.Runtime2.modules.assert.IsNull;
using AssertIsNotNull = PLang.Runtime2.modules.assert.IsNotNull;
using AssertContains = PLang.Runtime2.modules.assert.Contains;
using AssertGreaterThan = PLang.Runtime2.modules.assert.GreaterThan;
using AssertLessThan = PLang.Runtime2.modules.assert.LessThan;

namespace PLang.Tests.Runtime2.actions.assert;

public class AssertTests
{
    private (PLangContext context, MemoryStack memory) CreateContext()
    {
        var appContext = new PLangAppContext("/app");
        var memory = new MemoryStack();
        var context = new PLangContext(appContext, memory);
        var engine = new Engine(appContext);
        context.RegisterContextVariables(engine);
        return (context, memory);
    }

    // --- Equals ---

    [Test]
    public async Task Equals_SameInts_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = 42, Actual = 42 };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Equals_DifferentValues_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = 42, Actual = 99 };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error is AssertionError).IsTrue();
    }

    [Test]
    public async Task Equals_IntAndDouble_CoercesAndPasses()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = 5, Actual = 5.0 };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Equals_Strings_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = "hello", Actual = "hello" };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Equals_NullBothSides_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = null, Actual = null };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Equals_NullVsValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = null, Actual = 5 };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Equals_CustomMessage_IncludedInError()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = 1, Actual = 2, Message = "Sum check" };
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
        var action = new AssertNotEquals { Context = context, Expected = 1, Actual = 2 };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task NotEquals_SameValues_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertNotEquals { Context = context, Expected = 5, Actual = 5 };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    // --- IsTrue ---

    [Test]
    public async Task IsTrue_TrueValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = true };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task IsTrue_FalseValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = false };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task IsTrue_NonZeroNumber_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = 42 };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task IsTrue_Zero_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = 0 };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task IsTrue_Null_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = null };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    // --- IsFalse ---

    [Test]
    public async Task IsFalse_FalseValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsFalse { Context = context, Value = false };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task IsFalse_TrueValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsFalse { Context = context, Value = true };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task IsFalse_Null_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsFalse { Context = context, Value = null };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    // --- IsNull ---

    [Test]
    public async Task IsNull_NullValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsNull { Context = context, Value = null };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task IsNull_NonNullValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsNull { Context = context, Value = "hello" };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    // --- IsNotNull ---

    [Test]
    public async Task IsNotNull_NonNullValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsNotNull { Context = context, Value = "hello" };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task IsNotNull_NullValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsNotNull { Context = context, Value = null };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    // --- Contains ---

    [Test]
    public async Task Contains_StringContainsSubstring_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertContains { Context = context, Container = "hello world", Value = "world" };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Contains_StringDoesNotContain_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertContains { Context = context, Container = "hello world", Value = "xyz" };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Contains_ListContainsElement_Passes()
    {
        var (context, _) = CreateContext();
        var list = new List<object> { 1, 2, 3 };
        var action = new AssertContains { Context = context, Container = list, Value = 2 };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Contains_ListDoesNotContain_Fails()
    {
        var (context, _) = CreateContext();
        var list = new List<object> { 1, 2, 3 };
        var action = new AssertContains { Context = context, Container = list, Value = 99 };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Contains_NullContainer_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertContains { Context = context, Container = null, Value = "x" };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    // --- GreaterThan ---

    [Test]
    public async Task GreaterThan_LargerValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertGreaterThan { Context = context, A = 10, B = 5 };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task GreaterThan_EqualValues_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertGreaterThan { Context = context, A = 5, B = 5 };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task GreaterThan_SmallerValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertGreaterThan { Context = context, A = 3, B = 5 };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    // --- LessThan ---

    [Test]
    public async Task LessThan_SmallerValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertLessThan { Context = context, A = 3, B = 5 };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task LessThan_EqualValues_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertLessThan { Context = context, A = 5, B = 5 };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task LessThan_LargerValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertLessThan { Context = context, A = 10, B = 5 };
        var result = await action.Run();
        await Assert.That(result.Success).IsFalse();
    }
}
