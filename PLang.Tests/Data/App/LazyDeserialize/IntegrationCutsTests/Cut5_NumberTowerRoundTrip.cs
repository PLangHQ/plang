using System.Numerics;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using number = global::app.type.number.@this;
using PKind = global::app.type.number.NumberKind;
using PPolicy = global::app.type.number.NumberPolicy;

namespace PLang.Tests.App.LazyDeserialize.IntegrationCutsTests;

// Cut 5 — values across the full tower (sbyte/uint/ulong/Int128/
// BigInteger/Half/float/decimal) round-trip with their exact kind
// preserved. Arithmetic promotes-then-narrows; double⊕decimal raises.
public class Cut5_NumberTowerRoundTrip
{
    // A number serialized to its text form and read back through the
    // (number, kind) reader returns the EXACT CLR type — a float stays a
    // float (not double), a uint stays a uint. Exact-kind preservation is
    // the point of Decision 5.
    private static async Task RoundTrips(number n, System.Type expectedClr)
    {
        var r = number.Convert(n.ToString(), n.KindLabel, null!);
        await r.IsSuccess();
        await Assert.That(((global::app.type.number.@this)(await r.Value())!).BoxedValue.GetType()).IsEqualTo(expectedClr);
    }

    [Test] public async Task Cut5_RoundTrip_PreservesExactKind_AcrossTower()
    {
        await RoundTrips(number.From((sbyte)-5), typeof(sbyte));
        await RoundTrips(number.From(3000000000u), typeof(uint));
        await RoundTrips(number.From(9000000000000000000ul), typeof(ulong));
        await RoundTrips(number.From(Int128.MaxValue), typeof(Int128));
        await RoundTrips(number.From(BigInteger.Parse("9999999999999999999999")), typeof(BigInteger));
        await RoundTrips(number.From((Half)1.5), typeof(Half));
        await RoundTrips(number.From(1.5f), typeof(float));
        await RoundTrips(number.From(1.25m), typeof(decimal));
    }

    // Marquee: uint+uint overflows uint but promote-then-narrow lands it on
    // long with no silent wrap.
    [Test] public async Task Cut5_PromoteThenNarrow_NoSilentWrap()
    {
        var r = number.Add(number.From(3000000000u), number.From(2000000000u), PPolicy.Lenient);
        await r.IsSuccess();
        await Assert.That((await r.Value())!.Kind).IsEqualTo(PKind.Long);
        await Assert.That((await r.Value())!.ToInt64()).IsEqualTo(5000000000L);
    }

    // Negative — the explicit-cast wall. double⊕decimal raises rather than
    // silently picking one carrier.
    [Test] public async Task Cut5_DoubleDecimal_RaisesExplicitCastError()
    {
        var r = number.Add(number.From(1.5d), number.From(0.1m), PPolicy.Lenient);
        await r.IsFailure();
        await Assert.That(r.Error?.Key).IsEqualTo("PrecisionMixRequiresChoice");
    }
}
