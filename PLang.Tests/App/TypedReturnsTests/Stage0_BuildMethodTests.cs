namespace PLang.Tests.App.TypedReturnsTests;

// Stage 0 — IClass.Build() interface + builder.validate plumbing.
// Architect: .bot/typed-action-returns/architect/stages.md (Stage 0, items 2-3)
// Plan: .bot/typed-action-returns/architect/plan.md (A.6)

public class Stage0_BuildMethodTests
{
    [Test]
    public async Task IClass_HasOptionalBuildMethod_ReturningTaskOfData()
        // Reflection: app.modules.IClass declares Build() : Task<Data>.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task IClass_BuildDefaultImpl_ReturnsDataOkNoValue()
        // A no-override action handler's Build() returns Data.Ok() (Success=true, Value=null).
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuilderValidate_CallsBuildOnEachAction_InOrder()
        // Wire a fake step with three actions; record the order Build() is invoked.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuilderValidate_BuildReturnsOkWithTypeName_SetsTerminalVariableSetType()
        // Test action whose Build() returns Data.Ok("foo") — terminal variable.set's Type slot becomes "foo".
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuilderValidate_BuildReturnsFail_SurfacesErrorAndFailsValidation()
        // Build() returns Data.Fail(err) — validate aggregates and fails; emit does not proceed.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuilderValidate_BuildReturnsBareOk_DoesNotTouchTerminalType()
        // Build() returns Data.Ok() with no value — terminal variable.set Type unchanged from LLM output.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task IClass_Build_IsOptional_HandlerWithoutOverrideCompiles()
        // Source-gen: a record :IClass without Build() override still compiles + behaves as default Ok().
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuilderValidate_OnlyOneTerminalVariableSetPerStep_LastInChainWins()
        // A step compiled to read.file | list.where | variable.set — Type lands on the trailing variable.set, not intermediate ops.
        => Assert.Fail("Not implemented");
}
