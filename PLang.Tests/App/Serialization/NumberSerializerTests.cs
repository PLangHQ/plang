namespace PLang.Tests.App.Serialization;

// plang-types — Stage 3
// app/types/number/serializer/Default.cs — (number, *) → writer.Int/Long/Decimal/Double/Float
// by Kind. Uniform across formats: number renders the same in every writer (the IWriter
// primitive vocabulary is the cross-format contract).

public class NumberSerializerTests
{
    [Test] public async Task Number_KindInt_Default_EmitsWriterInt()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Number_KindLong_Default_EmitsWriterLong()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Number_KindDecimal_Default_EmitsWriterDecimal()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Number_KindDouble_Default_EmitsWriterDouble()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Number_KindFloat_Default_EmitsWriterFloat()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Number_Wire_RoundTrip_PreservesValueAndKind()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Number_TextWriter_StarFallback_HitsDefault()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Number_Decimal_ShortestRoundTrip_NoTrailingZeros()
        => throw new global::System.NotImplementedException();
}
