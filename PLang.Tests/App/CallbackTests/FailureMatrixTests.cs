namespace PLang.Tests.App.CallbackTests;

public class FailureMatrixTests
{
    [Test]
    public async Task FailureMatrix_TamperedBytes_DetectedBySigningVerify_RaisesSignatureMismatch()
    {
        // Mutate any byte of the serialized payload; signing.verify in callback.run fails.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task FailureMatrix_ExpiredSignature_DetectedBySigningVerify_RaisesSignatureExpired()
    {
        // app.Callback.Signature.ExpiresInMs set; clock moves past Expires → verify fails.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task FailureMatrix_GoalFileDeletedBetweenIssueAndResume_RaisesReferentIntegrityError()
    {
        // Issue ErrorCallback; delete goal file from disk; resume → Call.Restore fails.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task FailureMatrix_GoalHashDiffers_RaisesCallbackGoalHashMismatch()
    {
        // Goal file present but prose changed → live.Hash != stub.Hash → typed error.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task FailureMatrix_ProviderDllMissing_RaisesReferentIntegrityError()
    {
        // Captured runtime registration source not loadable on resume.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task FailureMatrix_ProviderDefaultSelectionNameMissing_RaisesReferentIntegrityError()
    {
        // Captured default-selection name doesn't match any registered provider.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task FailureMatrix_IdentityNameUnresolvable_RaisesReferentIntegrityError()
    {
        // Identity.Name in Selections doesn't resolve via the Identity provider's
        // GetOrCreateDefaultAsync(name).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task FailureMatrix_DataReadDoesNotAutoVerify_AssertsAbsenceOfVerifyCall()
    {
        // Reading a Data instance (deserialize through PlangDataSerializer) does NOT
        // invoke signing.verify — verification is the consumer's explicit step.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
