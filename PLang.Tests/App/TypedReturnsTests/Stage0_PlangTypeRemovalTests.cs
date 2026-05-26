namespace PLang.Tests.App.TypedReturnsTests;

// Stage 0 — [PlangType] attribute removal + class-name derivation.
// Architect: .bot/typed-action-returns/architect/stages.md (Stage 0, item 1)

public class Stage0_PlangTypeRemovalTests
{
    [Test]
    public async Task PlangTypeAttribute_DoesNotExistAsType()
        => Assert.Fail("Not implemented");

    [Test]
    public async Task PlangTypeAttribute_NoSourceFileReferencesIt()
        => Assert.Fail("Not implemented");

    [Test]
    public async Task Results_PlangTypeName_DerivesFromClassNameLowercased()
        => Assert.Fail("Not implemented");

    [Test]
    public async Task Mock_PlangTypeName_DerivesFromClassName()
        => Assert.Fail("Not implemented");

    [Test]
    public async Task Test_PlangTypeName_DerivesFromClassName_AfterRename()
        => Assert.Fail("Not implemented");

    [Test]
    public async Task PlangTypeDerivation_OBPSingleNameFolders_UseFolderNameNotThisLiteral()
        => Assert.Fail("Not implemented");
}
