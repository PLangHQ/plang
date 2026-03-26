using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.condition;
using PLang.Runtime2.modules.condition.providers;

namespace PLang.Tests.Runtime2.Modules.condition;

public class DefaultEvaluatorTests
{
    private readonly DefaultEvaluator _eval = new();

    private static Data D(object? value) => value == null ? new Data("") : Data.Ok(value);

    private Data Eval(object? left, string op, object? right)
        => _eval.Evaluate(new Compare { Left = D(left), Operator = op, Right = D(right) });

    private Data EvalIf(object? left, string? op = null, object? right = null)
        => _eval.Evaluate(new If { Left = D(left), Operator = op, Right = D(right) });

    private bool IsTrue(Data result) => result.Success && (bool)result.Value!;
    private bool IsFalse(Data result) => result.Success && !(bool)result.Value!;

    // --- Evaluate() — All Operators ---

    [Test] public async Task Evaluate_Equals_SameInts() => await Assert.That(IsTrue(Eval(5, "==", 5))).IsTrue();
    [Test] public async Task Evaluate_Equals_DifferentInts() => await Assert.That(IsFalse(Eval(5, "==", 10))).IsTrue();
    [Test] public async Task Evaluate_NotEquals_Different() => await Assert.That(IsTrue(Eval(5, "!=", 10))).IsTrue();
    [Test] public async Task Evaluate_NotEquals_Same() => await Assert.That(IsFalse(Eval(5, "!=", 5))).IsTrue();
    [Test] public async Task Evaluate_GreaterThan_LeftBigger() => await Assert.That(IsTrue(Eval(10, ">", 5))).IsTrue();
    [Test] public async Task Evaluate_GreaterThan_Equal() => await Assert.That(IsFalse(Eval(5, ">", 5))).IsTrue();
    [Test] public async Task Evaluate_LessThan_LeftSmaller() => await Assert.That(IsTrue(Eval(3, "<", 5))).IsTrue();
    [Test] public async Task Evaluate_LessThan_Equal() => await Assert.That(IsFalse(Eval(5, "<", 5))).IsTrue();
    [Test] public async Task Evaluate_GreaterOrEqual_Equal() => await Assert.That(IsTrue(Eval(5, ">=", 5))).IsTrue();
    [Test] public async Task Evaluate_GreaterOrEqual_Smaller() => await Assert.That(IsFalse(Eval(3, ">=", 5))).IsTrue();
    [Test] public async Task Evaluate_LessOrEqual_Equal() => await Assert.That(IsTrue(Eval(5, "<=", 5))).IsTrue();

    [Test] public async Task Evaluate_Contains_Present() => await Assert.That(IsTrue(Eval("hello world", "contains", "world"))).IsTrue();
    [Test] public async Task Evaluate_Contains_Absent() => await Assert.That(IsFalse(Eval("hello world", "contains", "xyz"))).IsTrue();
    [Test] public async Task Evaluate_Contains_CaseInsensitive() => await Assert.That(IsTrue(Eval("hello world", "contains", "WORLD"))).IsTrue();

    [Test]
    public async Task Evaluate_Contains_CollectionElement()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(IsTrue(Eval(list, "contains", 2))).IsTrue();
    }

    [Test]
    public async Task Evaluate_Contains_CollectionMissing()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(IsFalse(Eval(list, "contains", 99))).IsTrue();
    }

    [Test]
    public async Task Evaluate_Contains_MixedNumeric_IntInLongList()
    {
        var list = new List<object> { 5L, 10L, 15L };
        await Assert.That(IsTrue(Eval(list, "contains", (int)5))).IsTrue();
    }

    [Test]
    public async Task Evaluate_Contains_MixedNumeric_LongInIntList()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(IsTrue(Eval(list, "contains", 2L))).IsTrue();
    }

    [Test]
    public async Task Evaluate_In_MixedNumeric_IntInLongList()
    {
        var list = new List<object> { 5L, 10L, 15L };
        await Assert.That(IsTrue(Eval((int)5, "in", list))).IsTrue();
    }

    [Test]
    public async Task Evaluate_In_MixedNumeric_LongInIntList()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(IsTrue(Eval(2L, "in", list))).IsTrue();
    }

    [Test] public async Task Evaluate_StartsWith_Match() => await Assert.That(IsTrue(Eval("hello world", "startswith", "hello"))).IsTrue();
    [Test] public async Task Evaluate_StartsWith_NoMatch() => await Assert.That(IsFalse(Eval("hello world", "startswith", "world"))).IsTrue();
    [Test] public async Task Evaluate_EndsWith_Match() => await Assert.That(IsTrue(Eval("hello world", "endswith", "world"))).IsTrue();

    [Test]
    public async Task Evaluate_In_Present()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(IsTrue(Eval(2, "in", list))).IsTrue();
    }

    [Test]
    public async Task Evaluate_In_Absent()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(IsFalse(Eval(5, "in", list))).IsTrue();
    }

    [Test] public async Task Evaluate_IsEmpty_EmptyList() => await Assert.That(IsTrue(Eval(new List<object>(), "isempty", null))).IsTrue();
    [Test] public async Task Evaluate_IsEmpty_NonEmpty() => await Assert.That(IsFalse(Eval(new List<object> { 1 }, "isempty", null))).IsTrue();
    [Test] public async Task Evaluate_IsEmpty_Null() => await Assert.That(IsTrue(Eval(null, "isempty", null))).IsTrue();

    // --- Logical & Unary ---

    [Test] public async Task Evaluate_Not_Truthy() => await Assert.That(IsFalse(Eval(true, "not", null))).IsTrue();
    [Test] public async Task Evaluate_Not_Falsy() => await Assert.That(IsTrue(Eval(null, "not", null))).IsTrue();
    [Test] public async Task Evaluate_And_BothTrue() => await Assert.That(IsTrue(Eval(true, "and", true))).IsTrue();
    [Test] public async Task Evaluate_And_OneFalse() => await Assert.That(IsFalse(Eval(true, "and", false))).IsTrue();
    [Test] public async Task Evaluate_Or_BothFalse() => await Assert.That(IsFalse(Eval(false, "or", false))).IsTrue();
    [Test] public async Task Evaluate_Or_OneTrue() => await Assert.That(IsTrue(Eval(true, "or", false))).IsTrue();

    // --- Type normalization ---

    [Test] public async Task Evaluate_IntVsLong() => await Assert.That(IsTrue(Eval((int)5, "==", (long)5))).IsTrue();
    [Test] public async Task Evaluate_IntVsDouble() => await Assert.That(IsTrue(Eval((int)5, ">", (double)4.5))).IsTrue();
    [Test] public async Task Evaluate_StringVsInt() => await Assert.That(IsTrue(Eval("5", "==", 5))).IsTrue();
    [Test] public async Task Evaluate_NullEqualsNull() => await Assert.That(IsTrue(Eval(null, "==", null))).IsTrue();
    [Test] public async Task Evaluate_NullNotEqualsValue() => await Assert.That(IsTrue(Eval(null, "!=", 5))).IsTrue();
    [Test] public async Task Evaluate_NullGreaterThan() => await Assert.That(IsFalse(Eval(null, ">", 5))).IsTrue();
    [Test] public async Task Evaluate_StringEquality_CaseInsensitive() => await Assert.That(IsTrue(Eval("Hello", "==", "hello"))).IsTrue();

    [Test]
    public async Task Evaluate_UnsupportedOperator_ReturnsError()
    {
        var result = Eval(1, "xor", 2);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("EvaluationError");
    }

    [Test]
    public async Task Evaluate_NonComparable_GreaterThan_ReturnsError()
    {
        var result = Eval(new object(), ">", 5);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("EvaluationError");
    }

    [Test] public async Task Evaluate_UnknownNumericType() => await Assert.That(IsTrue(Eval((ushort)5, "==", (ushort)5))).IsTrue();

    // --- IsTruthy via If (null operator) ---

    [Test] public async Task IsTruthy_Null() => await Assert.That(IsFalse(EvalIf(null))).IsTrue();
    [Test] public async Task IsTruthy_True() => await Assert.That(IsTrue(EvalIf(true))).IsTrue();
    [Test] public async Task IsTruthy_False() => await Assert.That(IsFalse(EvalIf(false))).IsTrue();
    [Test] public async Task IsTruthy_ZeroInt() => await Assert.That(IsFalse(EvalIf(0))).IsTrue();
    [Test] public async Task IsTruthy_NonZeroInt() => await Assert.That(IsTrue(EvalIf(42))).IsTrue();
    [Test] public async Task IsTruthy_ZeroLong() => await Assert.That(IsFalse(EvalIf(0L))).IsTrue();
    [Test] public async Task IsTruthy_ZeroDouble() => await Assert.That(IsFalse(EvalIf(0.0))).IsTrue();
    [Test] public async Task IsTruthy_EmptyString() => await Assert.That(IsFalse(EvalIf(""))).IsTrue();
    [Test] public async Task IsTruthy_Whitespace() => await Assert.That(IsFalse(EvalIf("  "))).IsTrue();
    [Test] public async Task IsTruthy_NonEmptyString() => await Assert.That(IsTrue(EvalIf("hello"))).IsTrue();
    [Test] public async Task IsTruthy_EmptyCollection() => await Assert.That(IsFalse(EvalIf(new List<int>()))).IsTrue();
    [Test] public async Task IsTruthy_NonEmptyCollection() => await Assert.That(IsTrue(EvalIf(new List<int> { 1 }))).IsTrue();
    [Test] public async Task IsTruthy_Object() => await Assert.That(IsTrue(EvalIf(new object()))).IsTrue();
}
