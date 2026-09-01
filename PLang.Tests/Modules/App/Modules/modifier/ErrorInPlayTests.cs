namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// The gates on reading <c>%!error%</c> off the call stack instead of a side slot.
/// The error is already recorded on the frame that failed; <c>CallStack.Error</c> walks
/// <c>Caller</c> outward and answers with the first frame holding an unrecovered one.
///
/// The two RED tests below are the architect's gates on deleting <c>app.Error</c>. They fail
/// for one reason: dispatch pops the failing action's frame before <c>error.handle</c> runs
/// recovery, so at the moment <c>%!error%</c> is read the error is no longer reachable from
/// any live frame. The fix belongs where the error is recorded at unwind — never in a new
/// slot — and is a design decision, not a line edit. Until it lands, <c>%!error%</c> stays
/// wired to <c>App.Error</c> and these stay red.
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
        List<global::app.goal.step.action.modifier.@this>? modifiers = null) =>
        new()
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["error"], Name = "throw",
            Parameter = new List<global::app.data.@this>
                { new("message", message, context: global::PLang.Tests.TestApp.SharedContext) },
            Modifier = modifiers ?? new List<global::app.goal.step.action.modifier.@this>()
        };

    private static global::app.goal.step.action.modifier.@this ErrorHandler(
        params (string name, object? value)[] parameters) =>
        new()
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["error"], Name = "handle",
            Parameter = parameters
                .Select(p => new global::app.data.@this(p.name, p.value,
                    context: global::PLang.Tests.TestApp.SharedContext)).ToList()
        };

    /// <summary>A recovery chain of one goal.call.</summary>
    private static List<PrAction> CallGoal(string goalName) => new()
    {
        new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["goal"], Name = "call",
            Parameter = new List<global::app.data.@this>
            {
                new("goalname", new Dictionary<string, object?> { ["name"] = goalName },
                    context: global::PLang.Tests.TestApp.SharedContext)
            }
        }
    };

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
        foreach (var a in actions) { TemplateStamp.Apply(a); a.Step = step; step.Action.Add(a); }
        goal.Step.Add(step);
        _app.Goal.Add(goal);
        return goal;
    }

    /// <summary>An action that copies %!error.Message% into the named variable.</summary>
    private static PrAction CaptureError(string varName) => new()
    {
        Module = global::PLang.Tests.TestApp.SharedContext.App.Module["variable"], Name = "set",
        Parameter = new List<global::app.data.@this>
        {
            new("name", "%" + varName + "%", new global::app.type.@this("variable"),
                context: global::PLang.Tests.TestApp.SharedContext),
            new("value", "%!error.Message%", context: global::PLang.Tests.TestApp.SharedContext)
        }
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

    // ── GATE 1 and GATE 2: RED. See the class comment. ────────────────────────────────

    /// <summary>
    /// GATE 1 — the pop-timing gate. The failing action pushes its own Call inside
    /// DispatchAsync, records the error there, and that frame is disposed on the way out.
    /// error.handle then runs recovery, by which point the error is on a frame no longer
    /// on the live chain — so the walk answers null and %!error% cannot be read off it.
    /// </summary>
    [Test]
    public async Task ErrorInPlay_SurvivesTheFailingFramesPop()
    {
        var action = TestAction.Create("error", "throw", ("message", "the original failure"));
        var seen = new List<global::app.error.IError?>();

        await using (var call = Ctx.CallStack.Push(action, Ctx.Variable))
            call.Errors.Add(new global::app.error.Error("the original failure"));

        // The frame has popped — this is exactly the moment error.handle runs recovery.
        seen.Add(Ctx.CallStack.Error);

        await Assert.That(seen[0]).IsNotNull();
        await Assert.That(seen[0]!.Message).IsEqualTo("the original failure");
    }

    /// <summary>
    /// GATE 2 — the end-to-end shape: %!error% inside a recovery chain is the error being
    /// recovered. Runs the real modifier fold, so it also pins that recovery gets the error
    /// through whatever mechanism %!error% is wired to.
    /// </summary>
    [Test]
    public async Task ErrorInPlay_DuringRecovery_IsTheErrorBeingRecovered()
    {
        RegisterGoal("Recover", CaptureError("seen"));

        var action = Throw("the original failure",
            modifiers: new List<global::app.goal.step.action.modifier.@this>
            {
                ErrorHandler(("action", CallGoal("Recover")), ("order", "GoalFirst"))
            });

        var result = await action.Run(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("seen"))?.ToString())
            .IsEqualTo("the original failure");
    }
}
