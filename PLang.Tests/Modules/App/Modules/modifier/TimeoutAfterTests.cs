using static PLang.Tests.TestAction;

namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// Tests for the timeout.after modifier handler.
/// Wraps an action with a CancellationTokenSource that fires after Ms milliseconds.
/// </summary>
public class TimeoutAfterTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/app");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private static global::app.goal.step.action.modifier.@this TimeoutModifier(int ms) => new()
    {
        Module = "timeout",
        ActionName = "after",
        Parameter = new List<global::app.data.@this> { new("ms", ms, context: global::PLang.Tests.TestApp.SharedContext) }
    };

    [Test]
    public async Task After_ActionCompletesBefore_PassesThroughResult()
    {
        var action = new PrAction
        {
            Module = "variable",
            ActionName = "set",
            Parameter = new List<global::app.data.@this>
            {
                new("name", "%fast%", new global::app.type.@this("variable"), context: Ctx), new("value", "done", context: Ctx)
            },
            Modifier = new List<global::app.goal.step.action.modifier.@this> { TimeoutModifier(5000) }
        };

        var result = await action.Run(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("fast"))).IsEqualTo("done");
    }

    [Test]
    public async Task After_ActionExceedsTimeout_Returns408Error()
    {
        var action = new PrAction
        {
            Module = "timer",
            ActionName = "sleep",
            Parameter = new List<global::app.data.@this> { new("ms", 5000, context: Ctx) },
            Modifier = new List<global::app.goal.step.action.modifier.@this> { TimeoutModifier(50) }
        };

        var result = await action.Run(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(408);
    }

    [Test]
    public async Task After_CancellationTokenPropagatedToAction()
    {
        // Token did propagate: sleep was cut short well before its 10s target
        var action = new PrAction
        {
            Module = "timer",
            ActionName = "sleep",
            Parameter = new List<global::app.data.@this> { new("ms", 10_000, context: Ctx) },
            Modifier = new List<global::app.goal.step.action.modifier.@this> { TimeoutModifier(30) }
        };

        var start = DateTimeOffset.UtcNow;
        var result = await action.Run(Ctx);
        var elapsed = DateTimeOffset.UtcNow - start;

        await Assert.That(elapsed.TotalMilliseconds).IsLessThan(2000);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
    }

    [Test]
    public async Task After_ParentCancellation_PropagatesException()
    {
        // Parent cancellation (not the timeout) bubbles up as OperationCanceledException.
        using var parentCts = new CancellationTokenSource();
        Ctx.PushCancellation(parentCts);
        parentCts.CancelAfter(30);

        var action = new PrAction
        {
            Module = "timer",
            ActionName = "sleep",
            Parameter = new List<global::app.data.@this> { new("ms", 10_000, context: Ctx) },
            Modifier = new List<global::app.goal.step.action.modifier.@this> { TimeoutModifier(5000) }
        };

        await Assert.That(async () => await action.Run(Ctx))
            .Throws<OperationCanceledException>();

        Ctx.PopCancellation();
    }

    [Test]
    public async Task After_ZeroMsTimeout_ImmediateTimeout()
    {
        var action = new PrAction
        {
            Module = "timer",
            ActionName = "sleep",
            Parameter = new List<global::app.data.@this> { new("ms", 1000, context: Ctx) },
            Modifier = new List<global::app.goal.step.action.modifier.@this> { TimeoutModifier(0) }
        };

        var result = await action.Run(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
    }

    [Test]
    public async Task After_InnerThrowsOCE_CatchFallbackReturnsTimeoutError()
    {
        // Triggers the catch(OperationCanceledException) fallback path (after.cs:45-51).
        // Inner func throws OCE directly instead of returning a failed Data result.
        var modifiers = new List<global::app.goal.step.action.modifier.@this>
        {
            new global::app.goal.step.action.modifier.@this
            {
                Module = "timeout", ActionName = "after",
                Parameter = new List<global::app.data.@this> { new("ms", 1, context: Ctx) }
            }
        };

        Func<Task<global::app.data.@this>> throwingInner = async () =>
        {
            await Task.Delay(500); // long enough for the 1ms timeout to fire
            throw new OperationCanceledException();
        };

        var (wrapped, _) = await modifiers[0].Wrap(throwingInner, Ctx);
        var result = await wrapped!();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(408);
    }

    [Test]
    public async Task After_NestedWithOtherModifiers_TimeoutWrapsOuter()
    {
        // timeout(50) wraps error(ignore) wraps timer.sleep(5000).
        // Sleep exceeds deadline → timeout fires inside error.handle's wrap → error.handle
        // sees the 408, IgnoreError = true → final result is Ok.
        var action = new PrAction
        {
            Module = "timer",
            ActionName = "sleep",
            Parameter = new List<global::app.data.@this> { new("ms", 5000, context: Ctx) },
            Modifier = new List<global::app.goal.step.action.modifier.@this>
            {
                TimeoutModifier(50),
                new global::app.goal.step.action.modifier.@this
                {
                    Module = "error", ActionName = "handle",
                    Parameter = new List<global::app.data.@this> { new("ignoreError", true, context: Ctx) }
                }
            }
        };

        var result = await action.Run(Ctx);

        await result.IsSuccess();
    }
}
