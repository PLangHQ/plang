namespace PLang.Tests.App.Types;

// plang-types — Stage 3
// number.Parse / TryParse / Resolve(string, context) — narrowest-fit, context-free.
// "5"→Int; "5.0"→Decimal; "5e0"→Double; "3000000000"→Long. Non-numeric → null/Data.Error.
// Resolve takes context for signature uniformity, NEVER stores it.

public class NumberParseTests
{
    [Test] public async Task Parse_PlainInt_IsInt()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Parse_TooBigForInt_PromotesToLong()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Parse_DecimalPoint_IsDecimal()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Parse_ScientificNotation_IsDouble()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Parse_Negative_PreservesSign()
        => throw new global::System.NotImplementedException();

    [Test] public async Task TryParse_NonNumeric_ReturnsFalse_OutputNull()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Resolve_Context_NotStored_OnInstance()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Resolve_EmptyString_ReturnsNull()
        => throw new global::System.NotImplementedException();
}
