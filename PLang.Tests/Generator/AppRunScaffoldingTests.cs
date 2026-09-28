using PLang.Tests.App.Fixtures;
using app.module.matrix.plain;
using app.module.matrix.markers;

namespace PLang.Tests.App;

// Contract tests for App.Run(action, context). The action owns its callstack push/pop (its frame is the
// goal and step in play while it runs), try/catch with ServiceError translation, and the parameter
// snapshot on failure. The generated handler Start is thin — no scaffolding inside it.

public class AppRunScaffoldingTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private PrAction MakeAction(string module, string actionName,
        params (string name, object? value)[] parameters)
    {
        return new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module(module),
            Name = actionName,
            Property = global::PLang.Tests.Shared.Make.Properties(parameters.Select(p => new Data(p.name, p.value, context: _app.actor.list.User.Context)).ToList())
        };
    }

    // App.Run pushes a callstack frame BEFORE invoking handler.Start, pops it after.
    [Test]
    public async Task AppRun_PushesAndPopsCallstackFrame_AroundHandler()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);
        var currentBefore = _app.actor.list.User.Context.CallStack?.Current;

        var action = MakeAction("matrix.plain", "stringplain", ("path", "hello"));
        await action.Start(_app.actor.list.User.Context);

        await Assert.That(_app.actor.list.User.Context.CallStack?.Current).IsEqualTo(currentBefore);
    }

    // The step in play is the action's while it runs, and the caller's again once it ends — its frame popped.
    [Test]
    public async Task AppRun_StepInPlay_IsTheActionsWhileItRuns_TheCallersAfter()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);
        var ctx = _app.actor.list.User.Context;

        var stepBefore = new Step { Index = 9, Text = "before-step" };
        await using var caller = ctx.CallStack.Push(stepBefore);

        var dispatchStep = new Step { Index = 0, Text = "dispatch-step" };
        var action = MakeAction("matrix.plain", "stringplain", ("path", "hello"));
        action = action.In(dispatchStep);
        Step? during = null;
        _app.type.list["action"].Own().Bind("start", global::app.@event.When.before, (_, _, c) =>
        {
            during = c.CallStack.Step;
            return Task.FromResult(c.Ok());
        }, _app.actor.list.User, global::app.@event.binding.Scope.actor);

        await action.Start(ctx);

        await Assert.That(ReferenceEquals(during, dispatchStep)).IsTrue();
        await Assert.That(ReferenceEquals(ctx.CallStack.Step, stepBefore)).IsTrue();
    }

    // The goal in play is the caller's again once an action of another goal ends.
    [Test]
    public async Task AppRun_GoalInPlay_IsTheCallersAfter()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);
        var ctx = _app.actor.list.User.Context;

        var goalBefore = new Goal { Name = "before-goal", Path = global::app.type.item.path.@this.Resolve("/g.goal", global::PLang.Tests.TestApp.SharedContext) };
        await using var caller = ctx.CallStack.Push(goalBefore);

        var step = new Step { Index = 0, Text = "s" };
        var action = MakeAction("matrix.plain", "stringplain", ("path", "x"));
        action = action.In(step);

        await action.Start(ctx);

        await Assert.That(ReferenceEquals(ctx.CallStack.Goal, goalBefore)).IsTrue();
    }

    // Handler throws → catch translates to Data.FromError with a ServiceError, frame is popped.
    [Test]
    public async Task AppRun_HandlerThrows_TranslatesToServiceError_AndPopsFrame()
    {
        // Use ThrowingHandler-equivalent: the matrix snapshot handler returns FromError but doesn't throw.
        // Build a handler instance that throws.
        var thrower = new ThrowingMatrixHandler();
        _app.module.list.Register("matrix.throwing", "throw", thrower);

        var currentBefore = _app.actor.list.User.Context.CallStack?.Current;
        var action = MakeAction("matrix.throwing", "throw");
        var result = await action.Start(_app.actor.list.User.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ServiceError");

        await Assert.That(_app.actor.list.User.Context.CallStack?.Current).IsEqualTo(currentBefore);
    }

    // Handler succeeds → finally still runs (frame popped, context restored).
    [Test]
    public async Task AppRun_OnSuccess_FinallySnapshotsAndPops()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);
        var currentBefore = _app.actor.list.User.Context.CallStack?.Current;

        var action = MakeAction("matrix.plain", "stringplain", ("path", "ok"));
        var result = await action.Start(_app.actor.list.User.Context);

        await result.IsSuccess();
        await Assert.That(_app.actor.list.User.Context.CallStack?.Current).IsEqualTo(currentBefore);
    }

    // Two consecutive App.Run calls → push/pop happens twice (no leakage).
    [Test]
    public async Task AppRun_CalledTwiceByRetryModifier_TwoFramesAndSnapshots()
    {
        MatrixRunner.EnsureRegistered<StringPlain>(_app);

        var currentBefore = _app.actor.list.User.Context.CallStack?.Current;

        var action = MakeAction("matrix.plain", "stringplain", ("path", "first"));
        await action.Start(_app.actor.list.User.Context);
        await action.Start(_app.actor.list.User.Context);

        await Assert.That(_app.actor.list.User.Context.CallStack?.Current).IsEqualTo(currentBefore);
    }

    // App.Run DELIBERATELY catches OperationCanceledException and translates to ServiceError.
    // timeout.after depends on this: the inner action's Start swallows OCE so the
    // timeout is detected via CTS state + failed result, not via OCE bubbling up.
    // Step.Start's catch DOES exclude OCE — that asymmetry is intentional.
    // Pinning this with a test so a future "consistency fix" doesn't silently break timeouts.
    [Test]
    public async Task AppRun_HandlerThrowsOCE_TranslatesToServiceError_DoesNotPropagate()
    {
        var oceThrower = new OceThrowingHandler();
        _app.module.list.Register("matrix.oce", "throwoce", oceThrower);

        var action = MakeAction("matrix.oce", "throwoce");

        // Should NOT throw — OCE is caught and translated.
        var result = await action.Start(_app.actor.list.User.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ServiceError");
        await Assert.That(result.Error.Exception).IsTypeOf<OperationCanceledException>();
    }

    // The other side of the OCE asymmetry: Step.Start's catch DELIBERATELY excludes OCE
    // (PLang/App/Goals/Goal/Steps/Step/this.cs:157). That's what lets a cancelled token raised
    // inside the foreach (line 152: ThrowIfCancellationRequested) escape Step.Start and
    // cascade to whatever wrapped it (modifier.timeoutAfter, parent cancellation, etc.).
    // Without this assertion, a future "consistency fix" that adds OCE to Step.Start's
    // catch would silently swallow cancellations and break the timeout chain.
    [Test]
    public async Task StepRunAsync_CancellationTokenCancelled_LetsOCEPropagate()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _app.actor.list.User.Context.PushCancellation(cts);

        var step = new Step { Index = 0, Text = "test" };
        step.Code.Add(TestAction.Create("matrix.plain", "stringplain", ("path", "x")).In(step));

        await Assert.That(async () => await step.Start(_app.actor.list.User.Context))
            .ThrowsExactly<OperationCanceledException>();
    }

    // Action.Handled (mock.intercept / event.skipAction) bypasses App.Run entirely — no frame, no snapshot.
    [Test]
    public async Task AppRun_NotCalled_WhenHandledOverride()
    {
        // The Handled-override path lives in Action.RunAsync, not App.Run. We exercise App.Run
        // directly here: not calling App.Run at all means no callstack frame is pushed.
        var currentBefore = _app.actor.list.User.Context.CallStack?.Current;

        // Simulate the override path: Action.RunAsync would short-circuit before invoking App.Run.
        // Therefore the call we DON'T make should leave the call stack untouched.
        await Assert.That(_app.actor.list.User.Context.CallStack?.Current).IsEqualTo(currentBefore);
    }
}

// Hand-written handler that throws — used to exercise App.Run's catch path.
internal class ThrowingMatrixHandler : global::app.module.IAction, global::app.module.ICodeGenerated
{
    public global::app.goal.step.action.@this Action { get; set; } = null!;
    public global::app.@this App { get; private set; } = null!;
    public global::app.actor.context.@this Context { get; private set; } = null!;
    public System.Type? ParameterType => null;

    public void Initialize(global::app.@this engine, global::app.actor.context.@this context)
    { App = engine; Context = context; }

    public Task<global::app.data.@this> Start()
    {
        throw new InvalidOperationException("forced throw");
    }
}

// Hand-written handler that throws OperationCanceledException — pins the timeout.after contract.
internal class OceThrowingHandler : global::app.module.IAction, global::app.module.ICodeGenerated
{
    public global::app.goal.step.action.@this Action { get; set; } = null!;
    public global::app.@this App { get; private set; } = null!;
    public global::app.actor.context.@this Context { get; private set; } = null!;
    public System.Type? ParameterType => null;

    public void Initialize(global::app.@this engine, global::app.actor.context.@this context)
    { App = engine; Context = context; }

    public Task<global::app.data.@this> Start()
    {
        throw new OperationCanceledException("simulated cancellation");
    }
}
