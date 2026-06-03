using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.IntegrationCutsTests;

// Cut 1 — the headline payoff. A Data read from a source, routed through
// a courier without any navigation/As<T>, and serialized back out: the
// bytes equal the original raw byte-for-byte (no parse-then-reserialize).
// `_value` was never materialized.
public class Cut1_VerbatimPassthrough
{
    [Test] public async Task Cut1_UntouchedConfigJson_SerializesByteIdentical() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Cut1_UntouchedWirePayload_SerializesByteIdentical() { throw new System.NotImplementedException("not implemented"); }

    // The same Data, once navigated, *does* materialise and round-trips
    // semantically (post-mutation rule: serialize switches to renderer).
    [Test] public async Task Cut1_NavigatedConfigJson_StillRoundTripsSemantically() { throw new System.NotImplementedException("not implemented"); }

    // Test-designer open item 4 — the probe mechanism. Suggest a debug-only
    // counter on `reader.@this` incremented per dispatch. Tests toggle it
    // via the existing debug seam; production cost is zero. Pin: the
    // counter is zero on the untouched-path test above.
    [Test] public async Task Cut1_ReaderProbeCount_StaysZero_OnUntouchedPath() { throw new System.NotImplementedException("not implemented"); }
}
