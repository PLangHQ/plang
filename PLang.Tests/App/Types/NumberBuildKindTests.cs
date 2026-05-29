namespace PLang.Tests.App.Types;

// plang-types — Stage 3
// number.Build(value) → kind — the build-time literal-shape rule.
// Decimal point → "decimal"; exponent → "double"; else int/long by fit.

public class NumberBuildKindTests
{
    [Test] public async Task Build_DecimalLiteral_ReturnsDecimal()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Build_IntegerLiteral_ReturnsInt()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Build_TooBigForInt_ReturnsLong()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Build_ExponentNotation_ReturnsDouble()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Build_StringValue_ReadsLikeLiteral()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Build_NonNumeric_ReturnsNull()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Build_NullValue_ReturnsNull()
        => throw new global::System.NotImplementedException();
}
