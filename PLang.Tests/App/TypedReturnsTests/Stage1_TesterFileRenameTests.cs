namespace PLang.Tests.App.TypedReturnsTests;

// Stage 1 — tester/File → tester/Test rename.
// Architect: .bot/typed-action-returns/architect/stages.md (Stage 1)
// Plan: .bot/typed-action-returns/architect/plan.md (A.7)

public class Stage1_TesterFileRenameTests
{
    [Test]
    public async Task TesterFile_ClassNoLongerExists()
        // typeof(app.tester.File) lookup returns null — the old name is gone.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task TesterTest_ClassExistsAtNewLocation()
        // typeof(app.tester.Test.@this) exists at PLang/app/tester/Test/this.cs.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task TesterTest_CarriesAllNineDomainFields()
        // PrPath, EntryGoalName, Status, Directory, Goal, GoalHash, BuilderVersion, Tags, StatusReason — all present.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task TesterTest_PlangTypeName_IsTest_Not_TestFile()
        // Derived name from class: "test". The old "testfile" string is gone everywhere.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task NoSourceFile_ReferencesTesterFile()
        // Grep-equivalent: no `app.tester.File` or `tester.File` in any .cs.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task NoSourceFile_ReferencesTestfileString()
        // Grep-equivalent: no `"testfile"` literal anywhere in PLang sources or Tests/.
        => Assert.Fail("Not implemented");
}
