using App.Engine.Variables;
using App.modules.condition;

namespace PLang.Tests.App.Modules.condition;

public class OperatorTests
{
    private static Data D(object? value) => value == null ? new Data("") : Data.Ok(value);

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

    // --- ValidValues ---

    [Test]
    public async Task ValidValues_ContainsAllOperators()
    {
        var values = Operator.ValidValues;
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

    // --- IObject interface ---

    [Test]
    public async Task ImplementsIObject()
    {
        var op = new Operator("==");
        await Assert.That(op is App.modules.IObject).IsTrue();
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
}
