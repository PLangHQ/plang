namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 5
// Vocabulary sweep: "envelope" leaves the codebase. PLang/app/data/this.Envelope.cs is
// renamed to this.Transport.cs; the Signature docstring no longer reads "data envelope".
//
// Pruned from the architect's matrix:
//  - row 5.2 (git grep returns no matches) — that's a build/CI grep, not a TUnit test
//  - row 5.4 (local variables in Wrap/Compress renamed) — same; the existing TUnit
//    suite plus the cuts will fail naturally if anything regresses behaviour
//  - rows 5.5 / 5.6 (projects build clean, existing tests pass) — those are
//    pipeline-level invariants, not unit tests
//
// Kept: 5.1 (file moved) and 5.3 (docstring rewrite).

public class TransportRenameTests
{
    // 5.1 — PLang/app/data/this.Transport.cs exists at the expected path.
    [Test] public async Task DataTransportFile_ExistsAtExpectedPath()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 5.1b — The old this.Envelope.cs file is gone.
    [Test] public async Task DataEnvelopeFile_NoLongerExistsAtOldPath()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 5.3 — Signature docstring no longer says "data envelope"; reads "cryptographic
    //        signature attached to a Data".
    [Test] public async Task SignatureType_XmlDoc_NoLongerMentionsEnvelope()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
