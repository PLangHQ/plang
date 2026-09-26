namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// <c>%!error%</c> reads the call stack. The error is already recorded on the frame that
/// failed; <c>CallStack.Error</c> walks <c>Caller</c> outward and answers with the first
/// frame holding an unrecovered one. Nothing stores it a second time.
///
/// This works because an action owns ONE frame for its whole run — its lifecycle events, its
/// modifiers, and its dispatch. A modifier recovering from a failure runs inside the frame
/// that failed, so the error is still on the live chain when the recovery body reads it, and
/// <c>Handled</c> on that frame is what takes it out of play.
/// </summary>
public class ErrorInPlayTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private static PrAction Throw(string message,
        global::app.goal.step.action.modifier.list.@this? modifiers = null) =>
        new()
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["error"], Name = "throw",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
                { new("message", message, context: global::PLang.Tests.TestApp.SharedContext) }),
            Modifier = modifiers ?? new global::app.goal.step.action.modifier.list.@this()
        };

    private static global::app.goal.step.action.modifier.@this ErrorHandler(
        params (string name, object? value)[] parameters) =>
        new()
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["on"], Name = "error",
            Property = global::PLang.Tests.Shared.Make.Properties(parameters
                .Select(p => new global::app.data.@this(p.name, p.value,
                    context: global::PLang.Tests.TestApp.SharedContext)).ToList())
        };

    /// <summary>An error handler whose recovery chain calls <paramref name="goalName"/>.
    /// Recovery actions are structure on the modifier, not one of its parameters.</summary>
    private static global::app.goal.step.action.modifier.@this ErrorHandlerCalling(
        string goalName, params (string name, object? value)[] parameters)
    {
        var handler = ErrorHandler(parameters);
        handler.Recovery.Add(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["goal"], Name = "call",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("Name", goalName, context: global::PLang.Tests.TestApp.SharedContext)
            })
        });
        return handler;
    }

    /// <summary>Registers a goal whose single step runs the given actions.</summary>
    private Goal RegisterGoal(string name, params PrAction[] actions)
    {
        var goal = new Goal
        {
            Name = name,
            Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", global::PLang.Tests.TestApp.SharedContext)
        };
        var step = new Step { Goal = goal, Text = $"step of {name}" };
        // The .pr load applies the template seam; without it a %var% parameter never resolves.
        foreach (var a in actions) { TemplateStamp.Apply(a); a.Step = step; step.Code.Add(a); }
        goal.Step.Add(step);
        _app.Goal.Add(goal);
        return goal;
    }

    /// <summary>An action that copies %!error.Message% into the named variable.</summary>
    private static PrAction CaptureError(string varName) => new()
    {
        Module = global::PLang.Tests.TestApp.SharedContext.App.Module["variable"], Name = "set",
        Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
        {
            new("name", "%" + varName + "%", new global::app.type.@this("variable"),
                context: global::PLang.Tests.TestApp.SharedContext),
            new("value", "%!error.Message%", context: global::PLang.Tests.TestApp.SharedContext)
        })
    };

    // ── The walk answers correctly on a live chain ────────────────────────────────────

    /// <summary>Nothing failed — nothing is in play. Pins that the walk answers null rather
    /// than the newest thing that ever went wrong on the run.</summary>
    [Test]
    public async Task ErrorInPlay_NothingFailed_IsNull()
    {
        await Assert.That(Ctx.CallStack.Error).IsNull();
    }

    /// <summary>A live frame holding an unrecovered error IS the answer.</summary>
    [Test]
    public async Task ErrorInPlay_LiveFrameHoldsError_AnswersThatError()
    {
        var action = TestAction.Create("variable", "set", ("name", "%x%"), ("value", "v"));
        var error = new global::app.error.Error("frame failed");

        await using var call = Ctx.CallStack.Push(action, Ctx.Variable);
        call.Errors.Add(error);

        await Assert.That(Ctx.CallStack.Error).IsEqualTo(error);
    }

    /// <summary>An inner frame's error shadows its caller's, and un-shadows when the inner
    /// frame pops. This is the nesting half of the walk, and it already holds.</summary>
    [Test]
    public async Task ErrorInPlay_InnerFrame_ShadowsCallerThenUnshadows()
    {
        var outerAction = TestAction.Create("variable", "set", ("name", "%a%"), ("value", "1"));
        var innerAction = TestAction.Create("variable", "set", ("name", "%b%"), ("value", "2"));
        var outerError = new global::app.error.Error("outer failed");
        var innerError = new global::app.error.Error("inner failed");

        await using var outer = Ctx.CallStack.Push(outerAction, Ctx.Variable);
        outer.Errors.Add(outerError);
        await Assert.That(Ctx.CallStack.Error).IsEqualTo(outerError);

        await using (var inner = Ctx.CallStack.Push(innerAction, Ctx.Variable))
        {
            inner.Errors.Add(innerError);
            await Assert.That(Ctx.CallStack.Error).IsEqualTo(innerError);   // shadowed
        }

        await Assert.That(Ctx.CallStack.Error).IsEqualTo(outerError);       // un-shadowed
    }

    /// <summary>Recovery turns the error off — Handled is what stops a frame answering, and
    /// the frame keeps the entry either way for the audit view.</summary>
    [Test]
    public async Task ErrorInPlay_FrameHandled_StopsAnswering()
    {
        var action = TestAction.Create("variable", "set", ("name", "%x%"), ("value", "v"));

        await using var call = Ctx.CallStack.Push(action, Ctx.Variable);
        call.Errors.Add(new global::app.error.Error("recovered later"));

        call.Handled = true;

        await Assert.That(Ctx.CallStack.Error).IsNull();
        await Assert.That(call.Errors.Count).IsEqualTo(1);   // still in the audit view
    }

    /// <summary>
    /// A popped frame takes its error out of play. An action that has finished is not "where
    /// we had the last error" — which is exactly why recovery has to run INSIDE the frame that
    /// failed rather than after it, and why the frame spans the action's modifiers.
    /// </summary>
    [Test]
    public async Task ErrorInPlay_PoppedFrame_NoLongerInPlay()
    {
        var action = TestAction.Create("error", "throw", ("message", "already finished"));

        await using (var call = Ctx.CallStack.Push(action, Ctx.Variable))
        {
            call.Errors.Add(new global::app.error.Error("already finished"));
            await Assert.That(Ctx.CallStack.Error).IsNotNull();
        }

        await Assert.That(Ctx.CallStack.Error).IsNull();
    }

    /// <summary>
    /// The end-to-end shape: %!error% inside a recovery chain is the error being recovered.
    /// Runs the real modifier fold, so it pins that the failing action's frame is still live
    /// while its on.error runs — the whole reason the frame spans the modifiers.
    /// </summary>
    [Test]
    public async Task ErrorInPlay_DuringRecovery_IsTheErrorBeingRecovered()
    {
        RegisterGoal("Recover", CaptureError("seen"));

        var action = Throw("the original failure",
            modifiers: new global::app.goal.step.action.modifier.list.@this
            {
                ErrorHandlerCalling("Recover", ("order", "GoalFirst"))
            });

        var result = await action.Run(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("seen"))?.ToString())
            .IsEqualTo("the original failure");
    }

    /// <summary>An action that copies %!error.Key% into the named variable.</summary>
    private static PrAction CaptureErrorKey(string varName) => new()
    {
        Module = global::PLang.Tests.TestApp.SharedContext.App.Module["variable"], Name = "set",
        Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
        {
            new("name", "%" + varName + "%", new global::app.type.@this("variable"),
                context: global::PLang.Tests.TestApp.SharedContext),
            new("value", "%!error.Key%", context: global::PLang.Tests.TestApp.SharedContext)
        })
    };

    /// <summary>
    /// A modifier's own verdict is in play for the recovery outside it. The deadline belongs to
    /// timeout.after, not to the sleep it cancelled, so the error an enclosing `on error` recovers
    /// from is the TIMEOUT — not the inner action's cancellation, and not nothing.
    /// <para>This is why the verdict is recorded on the action's frame rather than a frame of the
    /// modifier's own: a modifier's frame would already have popped by the time on.error reads
    /// the chain, and the walk never descends into closed children.</para>
    /// </summary>
    [Test]
    public async Task ErrorInPlay_DuringRecovery_IsTheModifiersOwnVerdict()
    {
        RegisterGoal("Recover", CaptureErrorKey("seenKey"));

        var ctx = global::PLang.Tests.TestApp.SharedContext;
        var sleep = new PrAction
        {
            Module = ctx.App.Module["timer"], Name = "sleep",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this> { new("ms", 3000L, context: ctx) })
        };
        // The slot folds index 0 outermost, so on.error wraps timeout.after wraps the sleep —
        // the recovery is outside the deadline and sees the verdict the deadline produced.
        sleep.Modifier.Add(ErrorHandlerCalling("Recover", ("order", "GoalFirst")));
        sleep.Modifier.Add(new global::app.goal.step.action.modifier.@this
        {
            Module = ctx.App.Module["timeout"], Name = "after",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this> { new("ms", 1L, context: ctx) })
        });

        var result = await sleep.Run(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("seenKey"))?.ToString()).IsEqualTo("Timeout");
    }
}
