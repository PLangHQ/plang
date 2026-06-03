using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.IntegrationCutsTests;

// Cut 3 — Decision 8 — signing verifies at the boundary, on the bytes,
// independent of materialisation. Nested signed Data round-trips through
// the type-driven reader, no longer through `LiftDataIfShaped`'s key-shape
// sniff.
public class Cut3_SignThenWireThenVerify
{
    [Test] public async Task Cut3_SignedData_VerifiesAgainstRaw_WithoutMaterialising() { throw new System.NotImplementedException("not implemented"); }

    // The case `LiftDataIfShaped` was covering. After deletion, the inner
    // signed Data is rebuilt by `Signature`'s own reader, and the inner
    // signature still reaches `signing.verify`.
    [Test] public async Task Cut3_NestedSignedData_InnerSignatureReachesVerify() { throw new System.NotImplementedException("not implemented"); }

    // Negative — a tampered raw fails verification. (The test mutates the
    // raw out-of-band before verify is called.)
    [Test] public async Task Cut3_TamperedRaw_FailsVerification() { throw new System.NotImplementedException("not implemented"); }
}
