using TUnit.Core;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 4: `Data.ShouldExit()` unifies the three distinct
/// stop-conditions (unhandled failure, Returned, Exit-typed) into one branch
/// for the step loop, `Step.RunAsync`, and `Goal.RunFrom`.
public class StepLoopShouldExitTests
{
    [Test] public Task ShouldExit_True_UnhandledFailure_SuccessFalseHandledFalse(){ Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task ShouldExit_False_HandledFailure_SuccessFalseHandledTrue() { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task ShouldExit_True_ReturnedTrue()                           { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task ShouldExit_True_ExitTypedResult()                        { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task ShouldExit_False_OkSuccessNonExitType()                  { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task StepLoop_ShortCircuits_OnShouldExitTrue()                { Assert.Fail("Not implemented"); return Task.CompletedTask; }
}
