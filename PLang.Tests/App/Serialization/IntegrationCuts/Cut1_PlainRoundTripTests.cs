namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-serialize-cleanup — Integration Cut 1: Plain Data round-trip with implicit signing.
//
// Setup: a stream-channel-backed-by-MemoryStream, Mime = "application/plang", actor
//        context wired so EnsureSigned can resolve a signing identity.
// Capture: await channel.WriteAsync(Data.Ok("hello", name: "greeting")); read back.
//
// Proves end-to-end:
//   - ISerializer input contract holds (Stage 1)
//   - wire converter Write/Read are symmetric (Stage 2)
//   - sign-if-missing fires automatically during the converter walk (Stage 2)
//   - canonicalization hash matches the wire shape (Stage 2 canonicalization fix)

public class Cut1_PlainRoundTripTests
{
    // Wire JSON has exactly four reserved top-level fields, no `properties` since none set.
    [Test] public async Task Cut1_WireJson_HasFourTopLevelFields_NameTypeValueSignature()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Read.Value.As<string>() == "hello"; Read.Name == "greeting".
    [Test] public async Task Cut1_ReadBack_PreservesValueAndName()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Read.Signature is populated (sign-if-missing fired during write).
    [Test] public async Task Cut1_ReadBack_SignaturePopulatedFromImplicitWriteSign()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // crypto.Verify(read) succeeds against the wire bytes that produced it.
    [Test] public async Task Cut1_CryptoVerify_SucceedsAgainstWireBytes()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
