using TUnit.Core;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 4: `Step.RunFrom(ctx, actionIdx)` and
/// `Goal.RunFrom(ctx, stepIdx, actionIdx)` — continuation helpers used by
/// `Snapshot.ResumeChain`. `Steps.RunAsync(ctx, fromIndex)` overload supports
/// the recursive resume.
public class GoalRunFromTests
{
    [Test] public Task StepRunFrom_Zero_RunsAllActions()                        { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task StepRunFrom_MidStep_RunsRemainingActionsOnly()           { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task GoalRunFrom_ResumesActionThenRemainingStepsInGoal()      { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task GoalRunFrom_ShortCircuits_OnExitTypedResume()            { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task StepsRunAsync_FromIndexOverload_SkipsEarlierSteps()      { Assert.Fail("Not implemented"); return Task.CompletedTask; }
}
