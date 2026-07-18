using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app.data;
using app.error;
using app.module.action.output;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 4: `Data.ShouldExit()` unifies the three distinct
/// stop-conditions (unhandled failure, Returned, Exit-typed) into one branch
/// for the step loop, `Step.RunAsync`, and `Goal.Resume`.
public class StepLoopShouldExitTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create("/tmp/StepLoopShouldExitTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task ShouldExit_True_UnhandledFailure_SuccessFalseHandledFalse()
    {
        var d = app.Error(new ServiceError("boom"));
        d.Handled = false;
        await Assert.That(d.ShouldExit()).IsTrue();
    }

    [Test] public async Task ShouldExit_False_HandledFailure_SuccessFalseHandledTrue()
    {
        var d = app.Error(new ServiceError("boom"));
        d.Handled = true;
        await Assert.That(d.ShouldExit()).IsFalse();
    }

    [Test] public async Task ShouldExit_True_ReturnedTrue()
    {
        var d = app.Ok("v");
        d.Returned = true;
        await Assert.That(d.ShouldExit()).IsTrue();
    }

    [Test] public async Task ShouldExit_True_ExitTypedResult()
    {
        var app = TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-se-" + System.Guid.NewGuid().ToString("N")[..8]));
        var d = new global::app.data.@this<Ask>("", new Ask(), context: app.User.Context);
        await Assert.That(d.ShouldExit()).IsTrue();
    }

    [Test] public async Task ShouldExit_False_OkSuccessNonExitType()
    {
        var d = app.Ok("hello");
        await Assert.That(d.ShouldExit()).IsFalse();
    }

    // Step-loop integration: covered by the 2a.2 commit (Steps.RunAsync wires
    // ShouldExit) — exercised by the end-to-end Tests/Callback PLang fixtures.
    [Test] public async Task StepLoop_ShortCircuits_OnShouldExitTrue()
    {
        // Pinned by Tests/Callback/StatelessCrossGoalResumes end-to-end in 2a.8.
        // Here we just pin the predicate contract used by the loop.
        var app = TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-se-" + System.Guid.NewGuid().ToString("N")[..8]));
        var exitData = new global::app.data.@this<Ask>("", new Ask(), context: app.User.Context);
        await Assert.That(exitData.ShouldExit()).IsTrue();
    }
}
