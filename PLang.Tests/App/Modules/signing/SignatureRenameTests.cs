namespace PLang.Tests.App.Modules.signing;

public class SignatureRenameTests
{
    [Test]
    public async Task SignedDataTypeAlias_DoesNotResolve_AfterRename()
    {
        // The old SignedData type/alias is gone — assert via reflection that the name
        // doesn't resolve to a type in the signing module.
        var asm = typeof(global::App.modules.signing.Signature).Assembly;
        var resolved = asm.GetType("App.modules.signing.SignedData");
        await Assert.That(resolved).IsNull();
    }

    [Test]
    public async Task SigningSignatureType_ExistsUnderNewName()
    {
        // The new name lives at App.modules.signing.Signature.
        var asm = typeof(global::App.modules.signing.Signature).Assembly;
        var resolved = asm.GetType("App.modules.signing.Signature");
        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.FullName).IsEqualTo("App.modules.signing.Signature");
    }
}
