using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.NumberTowerTests;

// Promote-then-narrow (Decision 5):
//   integers → promote to BigInteger, compute, narrow to a result kind.
//   binary floats (Half/float/double) → promote to double.
//   decimal → stays decimal.
//   integer ⊕ binary float → double (C#'s rule).
//   integer ⊕ decimal → decimal.
//   double ⊕ decimal → error, requires an explicit cast.
//   Result kind = the wider of the two operand kinds, widened further only
//   if the value overflows it.
public class NumberArithmeticTests
{
    [Test] public async Task IntPlusInt_StaysInt() { throw new System.NotImplementedException("not implemented"); }

    // The marquee no-wrap row. uint(3_000_000_000) + uint(2_000_000_000)
    // overflows uint; result lands as `long(5_000_000_000)`, not a wrapped
    // uint.
    [Test] public async Task UIntPlusUInt_PromotesAndNarrowsToLong_NoWrap() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task IntPlusFloat_PromotesToDouble() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task IntPlusDecimal_PromotesToDecimal() { throw new System.NotImplementedException("not implemented"); }

    // The "correct not easy" edge — C# forbids double⊕decimal without an
    // explicit cast; PLang follows.
    [Test] public async Task DoublePlusDecimal_RaisesExplicitCastError() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task DivisionProducingFraction_LandsOnDecimalOrDouble_PerOperandKinds() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task BigIntegerLossless_AcrossSumOfManyInts() { throw new System.NotImplementedException("not implemented"); }

    // Independent #10 — "narrowing only when value fits". decimal(0.1) +
    // decimal(10) stays decimal even though the result is small. The
    // pinned shape: narrow only when overflow would otherwise happen.
    [Test] public async Task Narrowing_OnlyWhenValueFits() { throw new System.NotImplementedException("not implemented"); }

    // Independent #11 — integer-tower associativity. `(a+b)+c == a+(b+c)`
    // across the same set of kinds. Catches a subtle promote-then-narrow
    // ordering bug.
    [Test] public async Task IntegerAssociativity_AcrossKinds() { throw new System.NotImplementedException("not implemented"); }
}
