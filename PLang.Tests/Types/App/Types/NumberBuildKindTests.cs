using number = global::app.type.item.number.@this;

namespace PLang.Tests.App.Types;

// plang-types — Stage 3
// number.Build(value) → kind — the build-time literal-shape rule.
// Decimal point → "decimal"; exponent → "double"; else int/long by fit.

public class NumberBuildKindTests
{
    [Test] public async Task Build_DecimalLiteral_ReturnsDecimal()
    {
        await Assert.That(number.Build(3.14m)).IsEqualTo("decimal");
        await Assert.That(number.Build("3.14")).IsEqualTo("decimal");
    }

    [Test] public async Task Build_IntegerLiteral_ReturnsInt()
    {
        await Assert.That(number.Build(42)).IsEqualTo("int");
        await Assert.That(number.Build("42")).IsEqualTo("int");
    }

    [Test] public async Task Build_TooBigForInt_ReturnsLong()
    {
        await Assert.That(number.Build(3000000000L)).IsEqualTo("long");
        await Assert.That(number.Build("3000000000")).IsEqualTo("long");
    }

    [Test] public async Task Build_ExponentNotation_ReturnsDouble()
    {
        await Assert.That(number.Build("5e10")).IsEqualTo("double");
        await Assert.That(number.Build(1.5)).IsEqualTo("double");
    }

    [Test] public async Task Build_StringValue_ReadsLikeLiteral()
    {
        await Assert.That(number.Build("0.5")).IsEqualTo("decimal");
        await Assert.That(number.Build("123")).IsEqualTo("int");
        await Assert.That(number.Build("1e10")).IsEqualTo("double");
    }

    [Test] public async Task Build_NonNumeric_ReturnsNull()
    {
        await Assert.That(number.Build("hello")).IsNull();
        await Assert.That(number.Build(true)).IsNull();
    }

    [Test] public async Task Build_NullValue_ReturnsNull()
        => await Assert.That(number.Build(null)).IsNull();
}
