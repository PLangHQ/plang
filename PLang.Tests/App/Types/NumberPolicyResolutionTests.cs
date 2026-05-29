namespace PLang.Tests.App.Types;

// plang-types — Stage 4
// Policy resolves through app.config: step (nullable action param) → context (ConfigScope)
// → parent contexts → App.Config.Defaults → record-default. Goal is NOT a policy carrier.
// Defaults are lenient (Promote, Double); strict (Throw, Decimal) is one set away.

public class NumberPolicyResolutionTests
{
    [Test] public async Task Resolve_StepLevel_OverridesContext()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Resolve_ContextLevel_OverridesAppDefault()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Resolve_AppDefault_FromAppConfigDefaults()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Resolve_RecordDefault_LenientPromoteDouble_WhenNothingSet()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Resolve_SubContext_ClimbsParent_InheritsParentSetting()
        => throw new global::System.NotImplementedException();

    [Test] public async Task NumberPolicy_IsReadonlyStruct_OverflowAndPrecisionAxes()
        => throw new global::System.NotImplementedException();

    [Test] public async Task NumberConfig_ImplementsIConfig_AppConfigForT_ResolvesIt()
        => throw new global::System.NotImplementedException();

    [Test] public async Task GoalDoesNotCarry_NumberPolicy_NoFieldOnGoalEntity()
        => throw new global::System.NotImplementedException();
}
