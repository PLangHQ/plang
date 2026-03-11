using PLang.Runtime2.modules.condition.providers;

namespace PLang.Tests.Runtime2.Modules.condition;

public class DefaultEvaluatorTests
{
    private readonly DefaultEvaluator _eval = new();

    // --- Batch 1: Evaluate() — All Operators ---

    [Test]
    public async Task Evaluate_Equals_SameInts_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate(5, "==", 5)).IsTrue();
    }

    [Test]
    public async Task Evaluate_Equals_DifferentInts_ReturnsFalse()
    {
        await Assert.That(_eval.Evaluate(5, "==", 10)).IsFalse();
    }

    [Test]
    public async Task Evaluate_NotEquals_DifferentValues_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate(5, "!=", 10)).IsTrue();
    }

    [Test]
    public async Task Evaluate_NotEquals_SameValues_ReturnsFalse()
    {
        await Assert.That(_eval.Evaluate(5, "!=", 5)).IsFalse();
    }

    [Test]
    public async Task Evaluate_GreaterThan_LeftBigger_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate(10, ">", 5)).IsTrue();
    }

    [Test]
    public async Task Evaluate_GreaterThan_Equal_ReturnsFalse()
    {
        await Assert.That(_eval.Evaluate(5, ">", 5)).IsFalse();
    }

    [Test]
    public async Task Evaluate_LessThan_LeftSmaller_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate(3, "<", 5)).IsTrue();
    }

    [Test]
    public async Task Evaluate_LessThan_Equal_ReturnsFalse()
    {
        await Assert.That(_eval.Evaluate(5, "<", 5)).IsFalse();
    }

    [Test]
    public async Task Evaluate_GreaterOrEqual_EqualValues_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate(5, ">=", 5)).IsTrue();
    }

    [Test]
    public async Task Evaluate_GreaterOrEqual_LeftSmaller_ReturnsFalse()
    {
        await Assert.That(_eval.Evaluate(3, ">=", 5)).IsFalse();
    }

    [Test]
    public async Task Evaluate_LessOrEqual_EqualValues_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate(5, "<=", 5)).IsTrue();
    }

    [Test]
    public async Task Evaluate_Contains_SubstringPresent_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate("hello world", "contains", "world")).IsTrue();
    }

    [Test]
    public async Task Evaluate_Contains_SubstringAbsent_ReturnsFalse()
    {
        await Assert.That(_eval.Evaluate("hello world", "contains", "xyz")).IsFalse();
    }

    [Test]
    public async Task Evaluate_Contains_CaseInsensitive_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate("hello world", "contains", "WORLD")).IsTrue();
    }

    [Test]
    public async Task Evaluate_StartsWith_MatchingPrefix_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate("hello world", "startswith", "hello")).IsTrue();
    }

    [Test]
    public async Task Evaluate_StartsWith_NonMatchingPrefix_ReturnsFalse()
    {
        await Assert.That(_eval.Evaluate("hello world", "startswith", "world")).IsFalse();
    }

    [Test]
    public async Task Evaluate_EndsWith_MatchingSuffix_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate("hello world", "endswith", "world")).IsTrue();
    }

    [Test]
    public async Task Evaluate_In_ValueInList_ReturnsTrue()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(_eval.Evaluate(2, "in", list)).IsTrue();
    }

    [Test]
    public async Task Evaluate_In_ValueNotInList_ReturnsFalse()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(_eval.Evaluate(5, "in", list)).IsFalse();
    }

    [Test]
    public async Task Evaluate_IsEmpty_EmptyList_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate(new List<object>(), "isempty", null)).IsTrue();
    }

    [Test]
    public async Task Evaluate_IsEmpty_NonEmptyList_ReturnsFalse()
    {
        await Assert.That(_eval.Evaluate(new List<object> { 1 }, "isempty", null)).IsFalse();
    }

    [Test]
    public async Task Evaluate_IsEmpty_NullValue_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate(null, "isempty", null)).IsTrue();
    }

    // --- Batch 2: Logical, Unary, Edge Cases ---

    [Test]
    public async Task Evaluate_Not_TruthyValue_ReturnsFalse()
    {
        await Assert.That(_eval.Evaluate(true, "not", null)).IsFalse();
    }

    [Test]
    public async Task Evaluate_Not_FalsyValue_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate(null, "not", null)).IsTrue();
    }

    [Test]
    public async Task Evaluate_And_BothTrue_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate(true, "and", true)).IsTrue();
    }

    [Test]
    public async Task Evaluate_And_OneFalse_ReturnsFalse()
    {
        await Assert.That(_eval.Evaluate(true, "and", false)).IsFalse();
    }

    [Test]
    public async Task Evaluate_Or_BothFalse_ReturnsFalse()
    {
        await Assert.That(_eval.Evaluate(false, "or", false)).IsFalse();
    }

    [Test]
    public async Task Evaluate_Or_OneTrueOneFalse_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate(true, "or", false)).IsTrue();
    }

    [Test]
    public async Task Evaluate_IntVsLong_NormalizesAndCompares()
    {
        await Assert.That(_eval.Evaluate((int)5, "==", (long)5)).IsTrue();
    }

    [Test]
    public async Task Evaluate_IntVsDouble_NormalizesAndCompares()
    {
        await Assert.That(_eval.Evaluate((int)5, ">", (double)4.5)).IsTrue();
    }

    [Test]
    public async Task Evaluate_StringVsInt_ConvertsStringAndCompares()
    {
        await Assert.That(_eval.Evaluate("5", "==", 5)).IsTrue();
    }

    [Test]
    public async Task Evaluate_NullEqualsNull_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate(null, "==", null)).IsTrue();
    }

    [Test]
    public async Task Evaluate_NullNotEqualsValue_ReturnsTrue()
    {
        await Assert.That(_eval.Evaluate(null, "!=", 5)).IsTrue();
    }

    [Test]
    public async Task Evaluate_NullGreaterThan_ReturnsFalse()
    {
        await Assert.That(_eval.Evaluate(null, ">", 5)).IsFalse();
    }

    [Test]
    public async Task Evaluate_StringEquality_CaseInsensitive()
    {
        await Assert.That(_eval.Evaluate("Hello", "==", "hello")).IsTrue();
    }

    [Test]
    public async Task Evaluate_UnsupportedOperator_ThrowsNotSupported()
    {
        await Assert.That(() => _eval.Evaluate(1, "xor", 2)).Throws<NotSupportedException>();
    }

    // --- Batch 3: IsTruthy() ---

    [Test]
    public async Task IsTruthy_Null_ReturnsFalse()
    {
        await Assert.That(_eval.IsTruthy(null)).IsFalse();
    }

    [Test]
    public async Task IsTruthy_BoolTrue_ReturnsTrue()
    {
        await Assert.That(_eval.IsTruthy(true)).IsTrue();
    }

    [Test]
    public async Task IsTruthy_BoolFalse_ReturnsFalse()
    {
        await Assert.That(_eval.IsTruthy(false)).IsFalse();
    }

    [Test]
    public async Task IsTruthy_ZeroInt_ReturnsFalse()
    {
        await Assert.That(_eval.IsTruthy(0)).IsFalse();
    }

    [Test]
    public async Task IsTruthy_NonZeroInt_ReturnsTrue()
    {
        await Assert.That(_eval.IsTruthy(42)).IsTrue();
    }

    [Test]
    public async Task IsTruthy_ZeroLong_ReturnsFalse()
    {
        await Assert.That(_eval.IsTruthy(0L)).IsFalse();
    }

    [Test]
    public async Task IsTruthy_ZeroDouble_ReturnsFalse()
    {
        await Assert.That(_eval.IsTruthy(0.0)).IsFalse();
    }

    [Test]
    public async Task IsTruthy_EmptyString_ReturnsFalse()
    {
        await Assert.That(_eval.IsTruthy("")).IsFalse();
    }

    [Test]
    public async Task IsTruthy_WhitespaceString_ReturnsFalse()
    {
        await Assert.That(_eval.IsTruthy("  ")).IsFalse();
    }

    [Test]
    public async Task IsTruthy_NonEmptyString_ReturnsTrue()
    {
        await Assert.That(_eval.IsTruthy("hello")).IsTrue();
    }

    [Test]
    public async Task IsTruthy_EmptyCollection_ReturnsFalse()
    {
        await Assert.That(_eval.IsTruthy(new List<int>())).IsFalse();
    }

    [Test]
    public async Task IsTruthy_NonEmptyCollection_ReturnsTrue()
    {
        await Assert.That(_eval.IsTruthy(new List<int> { 1 })).IsTrue();
    }

    [Test]
    public async Task IsTruthy_ArbitraryObject_ReturnsTrue()
    {
        await Assert.That(_eval.IsTruthy(new object())).IsTrue();
    }
}
