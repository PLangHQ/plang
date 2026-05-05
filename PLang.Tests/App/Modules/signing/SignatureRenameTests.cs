namespace PLang.Tests.App.Modules.signing;

public class SignatureRenameTests
{
    [Test]
    public async Task SignedDataTypeAlias_DoesNotResolve_AfterRename()
    {
        // The old SignedData type/alias is gone — assert via reflection that the name
        // doesn't resolve to a type in the signing module.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SigningSignatureType_ExistsUnderNewName()
    {
        // The new name lives at App.modules.signing.Signature (or Signature/@this folder).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
