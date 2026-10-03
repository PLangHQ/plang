using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app.data;
using app.error;
using app.module.output;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 4: `Data.ShouldExit()` unifies the three distinct
/// stop-conditions (unhandled failure, Returned, Exit-typed) into one branch
/// for the step loop, `Step.RunAsync`, and `Goal.Resume`.
public class StepLoopShouldExitTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/tmp/StepLoopShouldExitTests-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
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
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-se-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        // the goal's own ask, waiting for its answer, stops it; an ask that arrived from another plang flows through
        var d = new global::app.data.@this<Ask>("", new Ask { Waiting = true }, context: app.actor.list.User.Context);
        await Assert.That(d.ShouldExit()).IsTrue();
        var arrived = new global::app.data.@this<Ask>("", new Ask(), context: app.actor.list.User.Context);
        await Assert.That(arrived.ShouldExit()).IsFalse();
    }

    [Test] public async Task ShouldExit_False_OkSuccessNonExitType()
    {
        var d = app.Ok("hello");
        await Assert.That(d.ShouldExit()).IsFalse();
    }

    // Step-loop integration: covered by the 2a.2 commit (Steps.RunAsync wires
    // ShouldExit) — exercised by the end-to-end test/Callback PLang fixtures.
    [Test] public async Task StepLoop_ShortCircuits_OnShouldExitTrue()
    {
        // Pinned by test/Callback/StatelessCrossGoalResumes end-to-end in 2a.8.
        // Here we just pin the predicate contract used by the loop.
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-se-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var exitData = new global::app.data.@this<Ask>("", new Ask { Waiting = true }, context: app.actor.list.User.Context);
        await Assert.That(exitData.ShouldExit()).IsTrue();
    }
}
