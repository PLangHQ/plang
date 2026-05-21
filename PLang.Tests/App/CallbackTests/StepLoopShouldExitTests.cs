using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app.data;
using app.errors;
using app.modules.output;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 4: `Data.ShouldExit()` unifies the three distinct
/// stop-conditions (unhandled failure, Returned, Exit-typed) into one branch
/// for the step loop, `Step.RunAsync`, and `Goal.RunFrom`.
public class StepLoopShouldExitTests
{
    [Test] public async Task ShouldExit_True_UnhandledFailure_SuccessFalseHandledFalse()
    {
        var d = global::app.data.@this.FromError(new ServiceError("boom"));
        d.Handled = false;
        await Assert.That(d.ShouldExit()).IsTrue();
    }

    [Test] public async Task ShouldExit_False_HandledFailure_SuccessFalseHandledTrue()
    {
        var d = global::app.data.@this.FromError(new ServiceError("boom"));
        d.Handled = true;
        await Assert.That(d.ShouldExit()).IsFalse();
    }

    [Test] public async Task ShouldExit_True_ReturnedTrue()
    {
        var d = global::app.data.@this.Ok("v");
        d.Returned = true;
        await Assert.That(d.ShouldExit()).IsTrue();
    }

    [Test] public async Task ShouldExit_True_ExitTypedResult()
    {
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-se-" + System.Guid.NewGuid().ToString("N")[..8]));
        var d = new global::app.data.@this<Ask>("", new Ask()) { Context = app.User.Context };
        await Assert.That(d.ShouldExit()).IsTrue();
    }

    [Test] public async Task ShouldExit_False_OkSuccessNonExitType()
    {
        var d = global::app.data.@this.Ok("hello");
        await Assert.That(d.ShouldExit()).IsFalse();
    }

    // Step-loop integration: covered by the 2a.2 commit (Steps.RunAsync wires
    // ShouldExit) — exercised by the end-to-end Tests/Callback PLang fixtures.
    [Test] public async Task StepLoop_ShortCircuits_OnShouldExitTrue()
    {
        // Pinned by Tests/Callback/StatelessCrossGoalResumes end-to-end in 2a.8.
        // Here we just pin the predicate contract used by the loop.
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-se-" + System.Guid.NewGuid().ToString("N")[..8]));
        var exitData = new global::app.data.@this<Ask>("", new Ask()) { Context = app.User.Context };
        await Assert.That(exitData.ShouldExit()).IsTrue();
    }
}
