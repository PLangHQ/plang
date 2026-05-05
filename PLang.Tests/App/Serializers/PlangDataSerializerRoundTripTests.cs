namespace PLang.Tests.App.Serializers;

public class PlangDataSerializerRoundTripTests
{
    [Test]
    public async Task PlangDataSerializer_Write_EmitsTypePlusValuePlusSignature()
    {
        // application/plang+data wire shape is the full envelope: Type + Value + Signature.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task PlangDataSerializer_Write_TriggersLazySigning_OnFirstSignatureRead()
    {
        // Write reads data.Signature → first access populates via signing.SignAsync.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task PlangDataSerializer_RoundTrip_SignaturePopulatedUnverifiedOnRead()
    {
        // After reading the payload back, Data.Signature is populated (unverified).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task PlangDataSerializer_RoundTrip_DoesNotAutoVerify()
    {
        // Reading does NOT auto-verify; verification is the consumer's explicit step.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task PlangDataSerializer_HandlesApplicationPlangDataMimeType()
    {
        // Registered for application/plang+data; channels routing test pins this.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
