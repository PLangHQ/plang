namespace PLang.Tests.App;

// Contract tests for App.Run(action, context) — the scaffolding wrapper introduced in v4 Phase 3.
// v4 contract: App.Run owns callstack push/pop, save/restore Context.Step/Goal/Event,
//   try/catch/finally with ServiceError translation, frame.SnapshotVariables in finally.
// Generated handler ExecuteAsync is now thin — no scaffolding inside it.

public class AppRunScaffoldingTests
{
    // App.Run pushes a callstack frame BEFORE invoking handler.ExecuteAsync, pops it after.
    [Test] public async Task AppRun_PushesAndPopsCallstackFrame_AroundHandler() => Assert.Fail("Not implemented");

    // App.Run sets Context.Step = action.Step before handler runs; restores prior Step after.
    [Test] public async Task AppRun_SavesAndRestoresContextStep() => Assert.Fail("Not implemented");

    // Context.Goal is preserved (saved + restored) across the handler call.
    [Test] public async Task AppRun_SavesAndRestoresContextGoal() => Assert.Fail("Not implemented");

    // Context.Event is preserved across the handler call.
    [Test] public async Task AppRun_SavesAndRestoresContextEvent() => Assert.Fail("Not implemented");

    // Handler throws → catch translates to Data.FromError with a ServiceError, snapshot is attached, frame is popped.
    [Test] public async Task AppRun_HandlerThrows_TranslatesToServiceError_AndPopsFrame() => Assert.Fail("Not implemented");

    // Handler succeeds → finally still runs (snapshot, pop, restore) — frame.SnapshotVariables fires.
    [Test] public async Task AppRun_OnSuccess_FinallySnapshotsAndPops() => Assert.Fail("Not implemented");

    // Modifier retries dispatch twice → App.Run runs twice, two frame push/pops, two snapshots.
    [Test] public async Task AppRun_CalledTwiceByRetryModifier_TwoFramesAndSnapshots() => Assert.Fail("Not implemented");

    // Action.Handled (mock.intercept / event.skipAction) bypasses App.Run entirely — no frame, no snapshot.
    [Test] public async Task AppRun_NotCalled_WhenHandledOverride() => Assert.Fail("Not implemented");
}
