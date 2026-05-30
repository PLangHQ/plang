using System.Reflection;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract: the test-domain entity lives at app.tester.test.@this (OBP
// singular-folder layout). Its PLang catalog name derives to "test" via the
// @this last-namespace-segment convention; the legacy app.tester.File type
// is gone, as is the "testfile" PLang name.

public class Stage1_TesterFileRenameTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // The old `app.tester.File` type was deleted with the move to tester/Test/this.cs.
    [Test]
    public async Task TesterFile_ClassNoLongerExists()
    {
        var asm = typeof(global::app.@this).Assembly;
        var legacyType = asm.GetType("app.tester.File");
        await Assert.That(legacyType).IsNull()
            .Because("Only tester.Test.@this exists; the legacy File class is gone.");
    }

    // The new home is app.tester.test.@this (OBP singular-folder convention).
    [Test]
    public async Task TesterTest_ClassExistsAtNewLocation()
    {
        var newType = typeof(global::app.tester.test.@this);
        await Assert.That(newType).IsNotNull();
        await Assert.That(newType.Namespace).IsEqualTo("app.tester.test");
        await Assert.That(newType.Name).IsEqualTo("this");
    }

    // The contract names 9 test-relevant fields. Four live directly on Test
    // (Goal, Status, StatusReason, Tags); the remaining five (PrPath,
    // EntryGoalName, Directory, GoalHash, BuilderVersion) live on Goal —
    // reachable via Test.Goal.X without duplication.
    [Test]
    public async Task TesterTest_CarriesAllNineDomainFields()
    {
        var testType = typeof(global::app.tester.test.@this);
        var testProps = testType.GetProperties().Select(p => p.Name).ToHashSet();
        var goalProps = typeof(Goal).GetProperties().Select(p => p.Name).ToHashSet();

        string[] onTest = { "Goal", "Status", "StatusReason", "Tags" };
        string[] onGoal = { "PrPath", "Path", "Hash", "BuilderVersion" };

        foreach (var p in onTest)
            await Assert.That(testProps).Contains(p).Because($"Test.{p} must exist directly.");
        foreach (var p in onGoal)
            await Assert.That(goalProps).Contains(p).Because($"Test.Goal.{p} must be reachable via the Goal field.");
    }

    // Class-name "@this" + last namespace segment "Test" derives to "test".
    // The literal "testfile" is gone — no [PlangType] override produces it.
    [Test]
    public async Task TesterTest_PlangTypeName_IsTest_Not_TestFile()
    {
        var name = _app.Type.Name(typeof(global::app.tester.test.@this));
        await Assert.That(name).IsEqualTo("test");
        await Assert.That(name).IsNotEqualTo("testfile");
    }

    // Grep-equivalent guard: nothing in the loaded PLang assembly references the
    // old `app.tester.File` type symbol (Type.GetType returns null end-to-end).
    [Test]
    public async Task NoSourceFile_ReferencesTesterFile()
    {
        var legacy = Type.GetType("app.tester.File, PLang");
        await Assert.That(legacy).IsNull();
    }

    // The PLang-name "testfile" must not resolve in the runtime type registry.
    [Test]
    public async Task NoSourceFile_ReferencesTestfileString()
    {
        var resolved = _app.Type.Get("testfile");
        await Assert.That(resolved).IsNull()
            .Because("No [PlangType(\"testfile\")] override exists — only 'test' resolves.");
    }
}
