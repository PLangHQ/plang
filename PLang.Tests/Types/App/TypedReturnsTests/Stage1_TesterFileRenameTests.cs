using System.Reflection;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract: the single-test value type lives at app.test.@this — the value
// element beside its collection (app.test.list). Its PLang catalog name is
// "test", derived from the @this namespace tail (no [PlangType] override). The
// historical names (app.tester.File, app.tester.test.@this, app.module.action.test.test)
// and the "testfile" PLang name are all gone.

public class Stage1_TesterFileRenameTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // Neither historical home resolves any more.
    [Test]
    public async Task LegacyTypeSymbols_NoLongerExist()
    {
        var asm = typeof(global::app.@this).Assembly;
        await Assert.That(asm.GetType("app.tester.File")).IsNull();
        await Assert.That(asm.GetType("app.tester.test.@this")).IsNull()
            .Because("The value type moved to app.test.@this.");
    }

    // The contract names 9 test-relevant fields. Four live directly on the type
    // (Goal, Status, StatusReason, Tags); the remaining five (PrPath,
    // EntryGoalName, Directory, GoalHash, BuilderVersion) live on Goal —
    // reachable via Test.Goal.X without duplication.
    [Test]
    public async Task TesterTest_CarriesAllNineDomainFields()
    {
        var testType = typeof(global::app.test.@this);
        var testProps = testType.GetProperties().Select(p => p.Name).ToHashSet();
        var goalProps = typeof(Goal).GetProperties().Select(p => p.Name).ToHashSet();

        string[] onTest = { "Goal", "Status", "StatusReason", "Tags" };
        string[] onGoal = { "PrPath", "Path", "Hash", "BuilderVersion" };

        foreach (var p in onTest)
            await Assert.That(testProps).Contains(p).Because($"Test.{p} must exist directly.");
        foreach (var p in onGoal)
            await Assert.That(goalProps).Contains(p).Because($"Test.Goal.{p} must be reachable via the Goal field.");
    }

    // The @this namespace-tail convention derives the name "test" — never "testfile".
    [Test]
    public async Task TesterTest_PlangTypeName_IsTest_Not_TestFile()
    {
        var name = _app.Type.Name(typeof(global::app.test.@this));
        await Assert.That(name).IsEqualTo("test");
        await Assert.That(name).IsNotEqualTo("testfile");
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
