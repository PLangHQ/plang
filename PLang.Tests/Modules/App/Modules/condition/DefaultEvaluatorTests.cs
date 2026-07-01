using app.variable;
using app.module.condition;
using app.module.condition.code;

namespace PLang.Tests.App.Modules.condition;

public class DefaultEvaluatorTests : System.IAsyncDisposable
{
    private readonly Default _eval = new();
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/tmp/defeval-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private Data D(object? value) => value == null ? new Data("") : _app.User.Context.Ok(value);

    private Task<global::app.data.@this<global::app.type.@bool.@this>> Eval(object? left, string op, object? right)
        => _eval.Evaluate(new Compare { Context = _app.User.Context, Left = D(left), Operator = _app.User.Context.Ok<global::app.type.choice.@this<Operator>>((global::app.type.choice.@this<Operator>)new Operator(op)), Right = D(right) });

    private Task<global::app.data.@this<global::app.type.@bool.@this>> EvalIf(object? left, string op = "==", object? right = null)
        => _eval.Evaluate(new If { Context = _app.User.Context, Left = D(left), Operator = _app.User.Context.Ok<global::app.type.choice.@this<Operator>>((global::app.type.choice.@this<Operator>)new Operator(op)), Right = D(right) });

    private bool IsTrue(global::app.data.@this<global::app.type.@bool.@this> result) => result.Success && (result.Peek() as global::app.type.@bool.@this)?.Value == true;
    private bool IsFalse(global::app.data.@this<global::app.type.@bool.@this> result) => result.Success && (result.Peek() as global::app.type.@bool.@this)?.Value == false;

    // --- Evaluate() — All Operators ---

    [Test] public async Task Evaluate_Equals_SameInts() => await Assert.That(IsTrue(await Eval(5, "==", 5))).IsTrue();
    [Test] public async Task Evaluate_Equals_DifferentInts() => await Assert.That(IsFalse(await Eval(5, "==", 10))).IsTrue();
    [Test] public async Task Evaluate_NotEquals_Different() => await Assert.That(IsTrue(await Eval(5, "!=", 10))).IsTrue();
    [Test] public async Task Evaluate_NotEquals_Same() => await Assert.That(IsFalse(await Eval(5, "!=", 5))).IsTrue();
    [Test] public async Task Evaluate_GreaterThan_LeftBigger() => await Assert.That(IsTrue(await Eval(10, ">", 5))).IsTrue();
    [Test] public async Task Evaluate_GreaterThan_Equal() => await Assert.That(IsFalse(await Eval(5, ">", 5))).IsTrue();
    [Test] public async Task Evaluate_LessThan_LeftSmaller() => await Assert.That(IsTrue(await Eval(3, "<", 5))).IsTrue();
    [Test] public async Task Evaluate_LessThan_Equal() => await Assert.That(IsFalse(await Eval(5, "<", 5))).IsTrue();
    [Test] public async Task Evaluate_GreaterOrEqual_Equal() => await Assert.That(IsTrue(await Eval(5, ">=", 5))).IsTrue();
    [Test] public async Task Evaluate_GreaterOrEqual_Smaller() => await Assert.That(IsFalse(await Eval(3, ">=", 5))).IsTrue();
    [Test] public async Task Evaluate_LessOrEqual_Equal() => await Assert.That(IsTrue(await Eval(5, "<=", 5))).IsTrue();

    [Test] public async Task Evaluate_Contains_Present() => await Assert.That(IsTrue(await Eval("hello world", "contains", "world"))).IsTrue();
    [Test] public async Task Evaluate_Contains_Absent() => await Assert.That(IsFalse(await Eval("hello world", "contains", "xyz"))).IsTrue();
    [Test] public async Task Evaluate_Contains_CaseInsensitive() => await Assert.That(IsTrue(await Eval("hello world", "contains", "WORLD"))).IsTrue();

    [Test]
    public async Task Evaluate_Contains_CollectionElement()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(IsTrue(await Eval(list, "contains", 2))).IsTrue();
    }

    [Test]
    public async Task Evaluate_Contains_CollectionMissing()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(IsFalse(await Eval(list, "contains", 99))).IsTrue();
    }

    [Test]
    public async Task Evaluate_Contains_MixedNumeric_IntInLongList()
    {
        var list = new List<object> { 5L, 10L, 15L };
        await Assert.That(IsTrue(await Eval(list, "contains", (int)5))).IsTrue();
    }

    [Test]
    public async Task Evaluate_Contains_MixedNumeric_LongInIntList()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(IsTrue(await Eval(list, "contains", 2L))).IsTrue();
    }

    [Test]
    public async Task Evaluate_In_MixedNumeric_IntInLongList()
    {
        var list = new List<object> { 5L, 10L, 15L };
        await Assert.That(IsTrue(await Eval((int)5, "in", list))).IsTrue();
    }

    [Test]
    public async Task Evaluate_In_MixedNumeric_LongInIntList()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(IsTrue(await Eval(2L, "in", list))).IsTrue();
    }

    [Test] public async Task Evaluate_StartsWith_Match() => await Assert.That(IsTrue(await Eval("hello world", "startswith", "hello"))).IsTrue();
    [Test] public async Task Evaluate_StartsWith_NoMatch() => await Assert.That(IsFalse(await Eval("hello world", "startswith", "world"))).IsTrue();
    [Test] public async Task Evaluate_EndsWith_Match() => await Assert.That(IsTrue(await Eval("hello world", "endswith", "world"))).IsTrue();

    [Test]
    public async Task Evaluate_In_Present()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(IsTrue(await Eval(2, "in", list))).IsTrue();
    }

    [Test]
    public async Task Evaluate_In_Absent()
    {
        var list = new List<object> { 1, 2, 3 };
        await Assert.That(IsFalse(await Eval(5, "in", list))).IsTrue();
    }

    [Test] public async Task Evaluate_IsEmpty_EmptyList() => await Assert.That(IsTrue(await Eval(new List<object>(), "isempty", null))).IsTrue();
    [Test] public async Task Evaluate_IsEmpty_NonEmpty() => await Assert.That(IsFalse(await Eval(new List<object> { 1 }, "isempty", null))).IsTrue();
    [Test] public async Task Evaluate_IsEmpty_Null() => await Assert.That(IsTrue(await Eval(null, "isempty", null))).IsTrue();

    // --- Logical ---

    [Test] public async Task Evaluate_And_BothTrue() => await Assert.That(IsTrue(await Eval(true, "and", true))).IsTrue();
    [Test] public async Task Evaluate_And_OneFalse() => await Assert.That(IsFalse(await Eval(true, "and", false))).IsTrue();
    [Test] public async Task Evaluate_Or_BothFalse() => await Assert.That(IsFalse(await Eval(false, "or", false))).IsTrue();
    [Test] public async Task Evaluate_Or_OneTrue() => await Assert.That(IsTrue(await Eval(true, "or", false))).IsTrue();

    // --- Type normalization ---

    [Test] public async Task Evaluate_IntVsLong() => await Assert.That(IsTrue(await Eval((int)5, "==", (long)5))).IsTrue();
    [Test] public async Task Evaluate_IntVsDouble() => await Assert.That(IsTrue(await Eval((int)5, ">", (double)4.5))).IsTrue();
    [Test] public async Task Evaluate_StringVsInt() => await Assert.That(IsTrue(await Eval("5", "==", 5))).IsTrue();
    [Test] public async Task Evaluate_NullEqualsNull() => await Assert.That(IsTrue(await Eval(null, "==", null))).IsTrue();
    [Test] public async Task Evaluate_NullNotEqualsValue() => await Assert.That(IsTrue(await Eval(null, "!=", 5))).IsTrue();
    // Ordering a null: the boundary errors (anything vs null is Equal/NotEqual —
    // equality-comparable but never ordered), surfaced as an EvaluationError result.
    [Test] public async Task Evaluate_NullGreaterThan() => await Assert.That((await Eval(null, ">", 5)).Success).IsFalse();
    [Test] public async Task Evaluate_StringEquality_CaseInsensitive() => await Assert.That(IsTrue(await Eval("Hello", "==", "hello"))).IsTrue();

    [Test]
    public async Task Evaluate_UnsupportedOperator_ThrowsOnConstruction()
    {
        await Assert.That(() => new Operator("xor")).ThrowsException()
            .WithMessageMatching("*Unsupported operator*");
    }

    [Test]
    public async Task Evaluate_NonComparable_GreaterThan_ReturnsError()
    {
        var result = await Eval(new object(), ">", 5);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("EvaluationError");
    }

    [Test] public async Task Evaluate_UnknownNumericType() => await Assert.That(IsTrue(await Eval((ushort)5, "==", (ushort)5))).IsTrue();

    // --- Truthy via == true: bool left checks value, non-bool left checks IsInitialized ---

    [Test] public async Task Truthy_BoolTrue_IsTrue() => await Assert.That(IsTrue(await EvalIf(true, "==", true))).IsTrue();
    [Test] public async Task Truthy_BoolFalse_IsFalse() => await Assert.That(IsFalse(await EvalIf(false, "==", true))).IsTrue();
    [Test] public async Task Truthy_Int_IsInitialized() => await Assert.That(IsTrue(await EvalIf(42, "==", true))).IsTrue();
    [Test] public async Task Truthy_String_IsInitialized() => await Assert.That(IsTrue(await EvalIf("hello", "==", true))).IsTrue();
    [Test] public async Task Truthy_Null_NotInitialized() => await Assert.That(IsFalse(await EvalIf(null, "==", true))).IsTrue();

    // --- `if %path% exists` — path answers its own truthiness ---
    //
    // Before the fix, file.exists returned the path object and `if X exists`
    // compared a non-null object `== true` → always true. Now the path is
    // IBooleanResolvable and AsBooleanAsync() probes the filesystem, so the
    // condition reflects actual existence.

    // The paths carry a Context whose app root contains them — in-root, so
    // AsBooleanAsync's AuthGate auto-grants. A context-less
    // path can't be gated and isn't a shape production produces.
    [Test] public async Task IfExists_PathToExistingFile_IsTrue()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-eval-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        var app = new global::app.@this(root);
        var file = System.IO.Path.Combine(root, "present.txt");
        System.IO.File.WriteAllText(file, "x");
        var fp = new global::app.type.path.file.@this(file, app.User.Context);
        await Assert.That(IsTrue(await EvalIf(fp, "==", true))).IsTrue();
        System.IO.Directory.Delete(root, true);
    }

    [Test] public async Task IfExists_PathToMissingFile_IsFalse()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-eval-missing-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        var app = new global::app.@this(root);
        var missing = System.IO.Path.Combine(root, "not-here.txt");
        var fp = new global::app.type.path.file.@this(missing, app.User.Context);
        await Assert.That(IsFalse(await EvalIf(fp, "==", true))).IsTrue();
        System.IO.Directory.Delete(root, true);
    }
}
