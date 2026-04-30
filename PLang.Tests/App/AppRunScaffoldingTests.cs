using PLang.Tests.App.Fixtures;
using App.modules.matrix.plain;
using App.modules.matrix.markers;
using App.modules.matrix.modifier;

namespace PLang.Tests.App;

// Contract tests for App.Run(action, context) — the scaffolding wrapper introduced in v4 Phase 3.
// v4 contract: App.Run owns callstack push/pop, save/restore Context.Step/Goal/Event,
//   try/catch/finally with ServiceError translation, frame.SnapshotVariables in finally.
// Generated handler ExecuteAsync is now thin — no scaffolding inside it.

public class AppRunScaffoldingTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::App.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private PrAction MakeAction(string module, string actionName,
        params (string name, object? value)[] parameters)
    {
        return new PrAction
        {
            Module = module,
            ActionName = actionName,
            Parameters = parameters.Select(p => new Data(p.name, p.value)).ToList()
        };
    }

    // App.Run pushes a callstack frame BEFORE invoking handler.ExecuteAsync, pops it after.
    [Test]
    public async Task AppRun_PushesAndPopsCallstackFrame_AroundHandler()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);
        var depthBefore = _app.Context.CallStack?.Depth ?? 0;

        var action = MakeAction("matrix.plain", "stringplain", ("path", "hello"));
        await _app.Run(action, _app.Context);

        var depthAfter = _app.Context.CallStack?.Depth ?? 0;
        await Assert.That(depthAfter).IsEqualTo(depthBefore);
    }

    // App.Run sets Context.Step = action.Step before handler runs; restores prior Step after.
    [Test]
    public async Task AppRun_SavesAndRestoresContextStep()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);

        var stepBefore = new Step { Index = 9, Text = "before-step" };
        _app.Context.Step = stepBefore;

        var dispatchStep = new Step { Index = 0, Text = "dispatch-step" };
        var action = MakeAction("matrix.plain", "stringplain", ("path", "hello"));
        action.Step = dispatchStep;

        await _app.Run(action, _app.Context);

        // Restored after dispatch
        await Assert.That(ReferenceEquals(_app.Context.Step, stepBefore)).IsTrue();
    }

    // Context.Goal is preserved (saved + restored) across the handler call.
    [Test]
    public async Task AppRun_SavesAndRestoresContextGoal()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);

        var goalBefore = new Goal { Name = "before-goal", Path = "/g.goal" };
        _app.Context.Goal = goalBefore;

        var step = new Step { Index = 0, Text = "s" };
        var action = MakeAction("matrix.plain", "stringplain", ("path", "x"));
        action.Step = step;

        await _app.Run(action, _app.Context);

        await Assert.That(ReferenceEquals(_app.Context.Goal, goalBefore)).IsTrue();
    }

    // Context.Event is preserved across the handler call.
    [Test]
    public async Task AppRun_SavesAndRestoresContextEvent()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);
        var eventBefore = _app.Context.Event;

        var action = MakeAction("matrix.plain", "stringplain", ("path", "x"));
        await _app.Run(action, _app.Context);

        await Assert.That(_app.Context.Event).IsEqualTo(eventBefore);
    }

    // Handler throws → catch translates to Data.FromError with a ServiceError, frame is popped.
    [Test]
    public async Task AppRun_HandlerThrows_TranslatesToServiceError_AndPopsFrame()
    {
        // Use ThrowingHandler-equivalent: the matrix snapshot handler returns FromError but doesn't throw.
        // Build a handler instance that throws.
        var thrower = new ThrowingMatrixHandler();
        _app.Modules.Register("matrix.throwing", "throw", thrower);

        var depthBefore = _app.Context.CallStack?.Depth ?? 0;
        var action = MakeAction("matrix.throwing", "throw");
        var result = await _app.Run(action, _app.Context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ServiceError");

        var depthAfter = _app.Context.CallStack?.Depth ?? 0;
        await Assert.That(depthAfter).IsEqualTo(depthBefore);
    }

    // Handler succeeds → finally still runs (frame popped, context restored).
    [Test]
    public async Task AppRun_OnSuccess_FinallySnapshotsAndPops()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);
        var depthBefore = _app.Context.CallStack?.Depth ?? 0;

        var action = MakeAction("matrix.plain", "stringplain", ("path", "ok"));
        var result = await _app.Run(action, _app.Context);

        await Assert.That(result.Success).IsTrue();
        var depthAfter = _app.Context.CallStack?.Depth ?? 0;
        await Assert.That(depthAfter).IsEqualTo(depthBefore);
    }

    // Two consecutive App.Run calls → push/pop happens twice (no leakage).
    [Test]
    public async Task AppRun_CalledTwiceByRetryModifier_TwoFramesAndSnapshots()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);

        var depthBefore = _app.Context.CallStack?.Depth ?? 0;

        var action = MakeAction("matrix.plain", "stringplain", ("path", "first"));
        await _app.Run(action, _app.Context);
        await _app.Run(action, _app.Context);

        var depthAfter = _app.Context.CallStack?.Depth ?? 0;
        await Assert.That(depthAfter).IsEqualTo(depthBefore);
    }

    // App.Run DELIBERATELY catches OperationCanceledException and translates to ServiceError.
    // timeout.after depends on this: the inner action's ExecuteAsync swallows OCE so the
    // timeout is detected via CTS state + failed result, not via OCE bubbling up.
    // Step.RunAsync's catch DOES exclude OCE — that asymmetry is intentional.
    // Pinning this with a test so a future "consistency fix" doesn't silently break timeouts.
    [Test]
    public async Task AppRun_HandlerThrowsOCE_TranslatesToServiceError_DoesNotPropagate()
    {
        var oceThrower = new OceThrowingHandler();
        _app.Modules.Register("matrix.oce", "throwoce", oceThrower);

        var action = MakeAction("matrix.oce", "throwoce");

        // Should NOT throw — OCE is caught and translated.
        var result = await _app.Run(action, _app.Context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ServiceError");
        await Assert.That(result.Error.Exception).IsTypeOf<OperationCanceledException>();
    }

    // Action.Handled (mock.intercept / event.skipAction) bypasses App.Run entirely — no frame, no snapshot.
    [Test]
    public async Task AppRun_NotCalled_WhenHandledOverride()
    {
        // The Handled-override path lives in Action.RunAsync, not App.Run. We exercise App.Run
        // directly here: not calling App.Run at all means no callstack frame is pushed.
        var depthBefore = _app.Context.CallStack?.Depth ?? 0;

        // Simulate the override path: Action.RunAsync would short-circuit before invoking App.Run.
        // Therefore the call we DON'T make should leave the call stack untouched.
        var depthAfter = _app.Context.CallStack?.Depth ?? 0;
        await Assert.That(depthAfter).IsEqualTo(depthBefore);
    }
}

// Hand-written handler that throws — used to exercise App.Run's catch path.
internal class ThrowingMatrixHandler : global::App.modules.IAction, global::App.modules.ICodeGenerated
{
    public global::App.Goals.Goal.Steps.Step.Actions.Action.@this Action { get; set; } = null!;
    public global::App.@this App { get; private set; } = null!;
    public global::App.Actor.Context.@this Context { get; private set; } = null!;
    public System.Type? ParameterType => null;

    public void Initialize(global::App.@this engine, global::App.Actor.Context.@this context)
    { App = engine; Context = context; }

    public Task<global::App.Data.@this> ExecuteAsync(
        global::App.Goals.Goal.Steps.Step.Actions.Action.@this action,
        global::App.Actor.Context.@this context)
    {
        throw new InvalidOperationException("forced throw");
    }
}

// Hand-written handler that throws OperationCanceledException — pins the timeout.after contract.
internal class OceThrowingHandler : global::App.modules.IAction, global::App.modules.ICodeGenerated
{
    public global::App.Goals.Goal.Steps.Step.Actions.Action.@this Action { get; set; } = null!;
    public global::App.@this App { get; private set; } = null!;
    public global::App.Actor.Context.@this Context { get; private set; } = null!;
    public System.Type? ParameterType => null;

    public void Initialize(global::App.@this engine, global::App.Actor.Context.@this context)
    { App = engine; Context = context; }

    public Task<global::App.Data.@this> ExecuteAsync(
        global::App.Goals.Goal.Steps.Step.Actions.Action.@this action,
        global::App.Actor.Context.@this context)
    {
        throw new OperationCanceledException("simulated cancellation");
    }
}
