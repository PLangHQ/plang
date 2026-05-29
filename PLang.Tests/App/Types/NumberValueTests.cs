namespace PLang.Tests.App.Types;

// plang-types — Stage 3
// app/types/number/this.cs — sealed class @this : IEquatable<@this>, IBooleanResolvable.
// Immutable; readonly slots _i / _d / _f; NumberKind { Int, Long, Float, Double, Decimal }.
// int/long/decimal/double/float are KINDS of number — not separate top-level types.
// No Context, no IContext stored.

public class NumberValueTests
{
    [Test] public async Task From_Int_StoresKindInt()
        => throw new global::System.NotImplementedException();

    [Test] public async Task From_Long_StoresKindLong()
        => throw new global::System.NotImplementedException();

    [Test] public async Task From_Decimal_StoresKindDecimal()
        => throw new global::System.NotImplementedException();

    [Test] public async Task From_Float_StoresKindFloat()
        => throw new global::System.NotImplementedException();

    [Test] public async Task From_Double_StoresKindDouble()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Implicit_InFromConcrete_AllFiveKinds_Compiles()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Explicit_OutToConcrete_LossyNarrowing_Throws()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Explicit_OutToConcrete_InRange_RoundTrips()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Explicit_IntCast_OnNaN_Throws()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Immutable_NoPublicSetters_AllSlotsReadonly()
        => throw new global::System.NotImplementedException();

    [Test] public async Task IBooleanResolvable_Zero_IsFalsy()
        => throw new global::System.NotImplementedException();

    [Test] public async Task IBooleanResolvable_NonZero_IsTruthy()
        => throw new global::System.NotImplementedException();

    [Test] public async Task IBooleanResolvable_NaN_IsFalsy()
        => throw new global::System.NotImplementedException();

    [Test] public async Task NumberDoesNotImplementOrStore_IContextOrContextReference()
        => throw new global::System.NotImplementedException();

    [Test] public async Task PlangTypeAttribute_Number_IsRegistered()
        => throw new global::System.NotImplementedException();
}
