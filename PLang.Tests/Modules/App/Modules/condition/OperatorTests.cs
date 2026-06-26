using app.variable;
using app.module.condition;

namespace PLang.Tests.App.Modules.condition;

public class OperatorTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/tmp/optests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private Data D(object? value) => value == null ? new Data("") : _app.User.Context.Ok(value);

    // --- Construction ---

    [Test]
    public async Task Constructor_ValidOperator_Succeeds()
    {
        var op = new Operator("==");
        await Assert.That(op.Value).IsEqualTo("==");
    }

    [Test]
    public async Task Constructor_CaseInsensitive()
    {
        var op = new Operator("CONTAINS");
        await Assert.That(op.Value).IsEqualTo("contains");
    }

    [Test]
    public async Task Constructor_InvalidOperator_Throws()
    {
        await Assert.That(() => new Operator("equals")).ThrowsException()
            .WithMessageMatching("*Unsupported operator*");
    }

    // --- Choices ---

    [Test]
    public async Task Choices_ContainsAllOperators()
    {
        var values = Operator.Choices(null);
        await Assert.That(values).Contains("==");
        await Assert.That(values).Contains("!=");
        await Assert.That(values).Contains(">");
        await Assert.That(values).Contains("<");
        await Assert.That(values).Contains(">=");
        await Assert.That(values).Contains("<=");
        await Assert.That(values).Contains("contains");
        await Assert.That(values).Contains("startswith");
        await Assert.That(values).Contains("endswith");
        await Assert.That(values).Contains("in");
        await Assert.That(values).Contains("isempty");
        await Assert.That(values).Contains("and");
        await Assert.That(values).Contains("or");
    }

    // --- Evaluate with Data ---

    [Test]
    public async Task Evaluate_Equals_SameInts()
    {
        var op = new Operator("==");
        await Assert.That(op.Evaluate(D(5), D(5))).IsTrue();
    }

    [Test]
    public async Task Evaluate_Equals_DifferentInts()
    {
        var op = new Operator("==");
        await Assert.That(op.Evaluate(D(5), D(10))).IsFalse();
    }

    [Test]
    public async Task Evaluate_Equals_True_WithBoolLeft()
    {
        var op = new Operator("==");
        await Assert.That(op.Evaluate(D(true), D(true))).IsTrue();
        await Assert.That(op.Evaluate(D(false), D(true))).IsFalse();
    }

    [Test]
    public async Task Evaluate_Equals_True_WithNonBoolLeft_ChecksIsInitialized()
    {
        var op = new Operator("==");
        // Non-bool value == true → checks IsInitialized
        await Assert.That(op.Evaluate(D(42), D(true))).IsTrue();
        await Assert.That(op.Evaluate(D("hello"), D(true))).IsTrue();
        // Null Data == true → not initialized
        await Assert.That(op.Evaluate(null, D(true))).IsFalse();
        // Uninitialized Data == true → false
        await Assert.That(op.Evaluate(new Data(""), D(true))).IsFalse();
    }

    [Test]
    public async Task Evaluate_Contains_CaseInsensitive()
    {
        var op = new Operator("contains");
        await Assert.That(op.Evaluate(D("Hello World"), D("WORLD"))).IsTrue();
    }

    [Test]
    public async Task Evaluate_GreaterThan()
    {
        var op = new Operator(">");
        await Assert.That(op.Evaluate(D(10), D(5))).IsTrue();
        await Assert.That(op.Evaluate(D(5), D(10))).IsFalse();
    }

    // --- Implicit conversion ---

    [Test]
    public async Task ImplicitConversion_ToString()
    {
        Operator op = new Operator("==");
        string value = op;
        await Assert.That(value).IsEqualTo("==");
    }

    [Test]
    public async Task ImplicitConversion_FromString()
    {
        Operator op = "contains";
        await Assert.That(op.Value).IsEqualTo("contains");
    }

    [Test]
    public async Task ImplicitConversion_FromString_Invalid_Throws()
    {
        await Assert.That(() => { Operator op = "equals"; }).ThrowsException();
    }

    // --- Enum ↔ string normalization (Operator.NormalizeTypes) ---
    // Used by `where Status equals 'Timeout'` patterns: PLang side has a string,
    // C# side has an enum value. Without normalization, Enum.Equals("Timeout") is
    // always false and the filter silently matches nothing.

    [Test]
    public async Task Equal_EnumLeft_StringRight_NormalizesToEnumName()
    {
        var op = new Operator("==");
        var matches = await op.Evaluate(D(global::app.tester.Status.Timeout), D("Timeout"));
        await Assert.That(matches).IsTrue();
    }

    [Test]
    public async Task Equal_StringLeft_EnumRight_NormalizesToEnumName()
    {
        var op = new Operator("==");
        var matches = await op.Evaluate(D("Fail"), D(global::app.tester.Status.Fail));
        await Assert.That(matches).IsTrue();
    }

    [Test]
    public async Task Equal_EnumVsMismatchedString_DoesNotMatch()
    {
        var op = new Operator("==");
        var matches = await op.Evaluate(D(global::app.tester.Status.Pass), D("Fail"));
        await Assert.That(matches).IsFalse();
    }
}
