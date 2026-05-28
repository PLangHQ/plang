namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-normalize — Integration Cut 3: Sign → wire → verify.
//
// After RawSignature is deleted, the seven migrated call sites must continue to produce
// a verifiable end-to-end chain: sign on the sending side, serialize through the new
// wire pipeline, deserialize on the receiver, verify the signature.

public class Cut3_SignWireVerifyTests
{
    [Test] public async Task Cut3_Sign_Serialize_Deserialize_Verify_Succeeds()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut3_Signature_BytesIntact_AfterJsonWriterRoundTrip()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut3_Ed25519_VerificationPath_WorksThroughSignatureAccessor()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut3_ActorPermission_SignVerify_Roundtrip_AfterMigration()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut3_PlangSerializer_SignVerify_Roundtrip_AfterMigration()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut3_TamperedBytes_AfterRoundTrip_FailVerification()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
