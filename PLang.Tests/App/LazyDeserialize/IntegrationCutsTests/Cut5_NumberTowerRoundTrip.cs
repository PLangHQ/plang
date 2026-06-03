using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.IntegrationCutsTests;

// Cut 5 — values across the full tower (sbyte/uint/ulong/Int128/
// BigInteger/Half/float/decimal) round-trip with their exact kind
// preserved. Arithmetic promotes-then-narrows; double⊕decimal raises.
public class Cut5_NumberTowerRoundTrip
{
    // Parametric over the tower. A `float` comes back `float`, not `double`;
    // a `uint` comes back `uint`. The exact-kind preservation is what
    // makes Decision 5 worth doing.
    [Test] public async Task Cut5_RoundTrip_PreservesExactKind_AcrossTower() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task Cut5_PromoteThenNarrow_NoSilentWrap() { throw new System.NotImplementedException("not implemented"); }

    // Negative — the explicit-cast wall. double⊕decimal raises rather than
    // silently picking one carrier.
    [Test] public async Task Cut5_DoubleDecimal_RaisesExplicitCastError() { throw new System.NotImplementedException("not implemented"); }
}
