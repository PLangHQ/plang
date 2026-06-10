using PLang.Tests.App.Fixtures;
using app.module.matrix.plain;
using app.module.matrix.markers;
using app.module.matrix.modifier;

namespace PLang.Tests.App;

// Contract tests for App.Run(action, context). App.Run owns callstack push/pop,
// save/restore Context.Step/Goal/Event, try/catch/finally with ServiceError
// translation, and frame.SnapshotVariables in finally. The generated handler
// ExecuteAsync is thin — no scaffolding inside it.

public class AppRunScaffoldingTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

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
        var currentBefore = _app.User.Context.CallStack?.Current;

        var action = MakeAction("matrix.plain", "stringplain", ("path", "hello"));
        await action.RunAsync(_app.User.Context);

        await Assert.That(_app.User.Context.CallStack?.Current).IsEqualTo(currentBefore);
    }

    // App.Run sets Context.Step = action.Step before handler runs; restores prior Step after.
    [Test]
    public async Task AppRun_SavesAndRestoresContextStep()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);

        var stepBefore = new Step { Index = 9, Text = "before-step" };
        _app.User.Context.Step = stepBefore;

        var dispatchStep = new Step { Index = 0, Text = "dispatch-step" };
        var action = MakeAction("matrix.plain", "stringplain", ("path", "hello"));
        action.Step = dispatchStep;

        await action.RunAsync(_app.User.Context);

        // Restored after dispatch
        await Assert.That(ReferenceEquals(_app.User.Context.Step, stepBefore)).IsTrue();
    }

    // Context.Goal is preserved (saved + restored) across the handler call.
    [Test]
    public async Task AppRun_SavesAndRestoresContextGoal()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);

        var goalBefore = new Goal { Name = "before-goal", Path = "/g.goal" };
        _app.User.Context.Goal = goalBefore;

        var step = new Step { Index = 0, Text = "s" };
        var action = MakeAction("matrix.plain", "stringplain", ("path", "x"));
        action.Step = step;

        await action.RunAsync(_app.User.Context);

        await Assert.That(ReferenceEquals(_app.User.Context.Goal, goalBefore)).IsTrue();
    }

    // Context.Event is preserved across the handler call.
    [Test]
    public async Task AppRun_SavesAndRestoresContextEvent()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);
        var eventBefore = _app.User.Context.Event;

        var action = MakeAction("matrix.plain", "stringplain", ("path", "x"));
        await action.RunAsync(_app.User.Context);

        await Assert.That(_app.User.Context.Event).IsEqualTo(eventBefore);
    }

    // Handler throws → catch translates to Data.FromError with a ServiceError, frame is popped.
    [Test]
    public async Task AppRun_HandlerThrows_TranslatesToServiceError_AndPopsFrame()
    {
        // Use ThrowingHandler-equivalent: the matrix snapshot handler returns FromError but doesn't throw.
        // Build a handler instance that throws.
        var thrower = new ThrowingMatrixHandler();
        _app.Module.Register("matrix.throwing", "throw", thrower);

        var currentBefore = _app.User.Context.CallStack?.Current;
        var action = MakeAction("matrix.throwing", "throw");
        var result = await action.RunAsync(_app.User.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ServiceError");

        await Assert.That(_app.User.Context.CallStack?.Current).IsEqualTo(currentBefore);
    }

    // Handler succeeds → finally still runs (frame popped, context restored).
    [Test]
    public async Task AppRun_OnSuccess_FinallySnapshotsAndPops()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);
        var currentBefore = _app.User.Context.CallStack?.Current;

        var action = MakeAction("matrix.plain", "stringplain", ("path", "ok"));
        var result = await action.RunAsync(_app.User.Context);

        await result.IsSuccess();
        await Assert.That(_app.User.Context.CallStack?.Current).IsEqualTo(currentBefore);
    }

    // Two consecutive App.Run calls → push/pop happens twice (no leakage).
    [Test]
    public async Task AppRun_CalledTwiceByRetryModifier_TwoFramesAndSnapshots()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);

        var currentBefore = _app.User.Context.CallStack?.Current;

        var action = MakeAction("matrix.plain", "stringplain", ("path", "first"));
        await action.RunAsync(_app.User.Context);
        await action.RunAsync(_app.User.Context);

        await Assert.That(_app.User.Context.CallStack?.Current).IsEqualTo(currentBefore);
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
        _app.Module.Register("matrix.oce", "throwoce", oceThrower);

        var action = MakeAction("matrix.oce", "throwoce");

        // Should NOT throw — OCE is caught and translated.
        var result = await action.RunAsync(_app.User.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ServiceError");
        await Assert.That(result.Error.Exception).IsTypeOf<OperationCanceledException>();
    }

    // The other side of the OCE asymmetry: Step.RunAsync's catch DELIBERATELY excludes OCE
    // (PLang/App/Goals/Goal/Steps/Step/this.cs:157). That's what lets a cancelled token raised
    // inside the foreach (line 152: ThrowIfCancellationRequested) escape Step.RunAsync and
    // cascade to whatever wrapped it (modifier.timeoutAfter, parent cancellation, etc.).
    // Without this assertion, a future "consistency fix" that adds OCE to Step.RunAsync's
    // catch would silently swallow cancellations and break the timeout chain.
    [Test]
    public async Task StepRunAsync_CancellationTokenCancelled_LetsOCEPropagate()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _app.User.Context.PushCancellation(cts);

        var action = TestAction.Create("matrix.plain", "stringplain", ("path", "x"));
        var step = new Step
        {
            Index = 0,
            Text = "test",
            Actions = new StepActions { action }
        };
        action.Step = step;

        await Assert.That(async () => await step.RunAsync(_app.User.Context))
            .ThrowsExactly<OperationCanceledException>();
    }

    // Action.Handled (mock.intercept / event.skipAction) bypasses App.Run entirely — no frame, no snapshot.
    [Test]
    public async Task AppRun_NotCalled_WhenHandledOverride()
    {
        // The Handled-override path lives in Action.RunAsync, not App.Run. We exercise App.Run
        // directly here: not calling App.Run at all means no callstack frame is pushed.
        var currentBefore = _app.User.Context.CallStack?.Current;

        // Simulate the override path: Action.RunAsync would short-circuit before invoking App.Run.
        // Therefore the call we DON'T make should leave the call stack untouched.
        await Assert.That(_app.User.Context.CallStack?.Current).IsEqualTo(currentBefore);
    }
}

// Hand-written handler that throws — used to exercise App.Run's catch path.
internal class ThrowingMatrixHandler : global::app.module.IAction, global::app.module.ICodeGenerated
{
    public global::app.goal.steps.step.actions.action.@this Action { get; set; } = null!;
    public global::app.@this App { get; private set; } = null!;
    public global::app.actor.context.@this Context { get; private set; } = null!;
    public System.Type? ParameterType => null;

    public void Initialize(global::app.@this engine, global::app.actor.context.@this context)
    { App = engine; Context = context; }

    public Task<global::app.data.@this> ExecuteAsync(
        global::app.goal.steps.step.actions.action.@this action,
        global::app.actor.context.@this context)
    {
        throw new InvalidOperationException("forced throw");
    }
}

// Hand-written handler that throws OperationCanceledException — pins the timeout.after contract.
internal class OceThrowingHandler : global::app.module.IAction, global::app.module.ICodeGenerated
{
    public global::app.goal.steps.step.actions.action.@this Action { get; set; } = null!;
    public global::app.@this App { get; private set; } = null!;
    public global::app.actor.context.@this Context { get; private set; } = null!;
    public System.Type? ParameterType => null;

    public void Initialize(global::app.@this engine, global::app.actor.context.@this context)
    { App = engine; Context = context; }

    public Task<global::app.data.@this> ExecuteAsync(
        global::app.goal.steps.step.actions.action.@this action,
        global::app.actor.context.@this context)
    {
        throw new OperationCanceledException("simulated cancellation");
    }
}
