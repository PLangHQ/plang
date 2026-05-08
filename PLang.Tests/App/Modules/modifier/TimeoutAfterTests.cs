using static PLang.Tests.TestAction;

namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// Tests for the timeout.after modifier handler.
/// Wraps an action with a CancellationTokenSource that fires after Ms milliseconds.
/// </summary>
public class TimeoutAfterTests
{
    private global::App.@this _app = null!;
    private global::App.Actor.Context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/app");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private static PrAction TimeoutModifier(int ms) => new()
    {
        Module = "timeout",
        ActionName = "after",
        Parameters = new List<global::App.Data.@this> { new("ms", ms) }
    };

    [Test]
    public async Task After_ActionCompletesBefore_PassesThroughResult()
    {
        var action = new PrAction
        {
            Module = "variable",
            ActionName = "set",
            Parameters = new List<global::App.Data.@this>
            {
                new("name", "%fast%"), new("value", "done")
            },
            Modifiers = new ActionModifiers { TimeoutModifier(5000) }
        };

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(Ctx.Variables.GetValue("fast")).IsEqualTo("done");
    }

    [Test]
    public async Task After_ActionExceedsTimeout_Returns408Error()
    {
        var action = new PrAction
        {
            Module = "timer",
            ActionName = "sleep",
            Parameters = new List<global::App.Data.@this> { new("ms", 5000) },
            Modifiers = new ActionModifiers { TimeoutModifier(50) }
        };

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsFalse();
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
            Parameters = new List<global::App.Data.@this> { new("ms", 10_000) },
            Modifiers = new ActionModifiers { TimeoutModifier(30) }
        };

        var start = DateTimeOffset.UtcNow;
        var result = await action.RunAsync(Ctx);
        var elapsed = DateTimeOffset.UtcNow - start;

        await Assert.That(elapsed.TotalMilliseconds).IsLessThan(2000);
        await Assert.That(result.Success).IsFalse();
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
            Parameters = new List<global::App.Data.@this> { new("ms", 10_000) },
            Modifiers = new ActionModifiers { TimeoutModifier(5000) }
        };

        await Assert.That(async () => await action.RunAsync(Ctx))
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
            Parameters = new List<global::App.Data.@this> { new("ms", 1000) },
            Modifiers = new ActionModifiers { TimeoutModifier(0) }
        };

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
    }

    [Test]
    public async Task After_InnerThrowsOCE_CatchFallbackReturnsTimeoutError()
    {
        // Triggers the catch(OperationCanceledException) fallback path (after.cs:45-51).
        // Inner func throws OCE directly instead of returning a failed Data result.
        var modifiers = new ActionModifiers
        {
            new PrAction
            {
                Module = "timeout", ActionName = "after",
                Parameters = new List<global::App.Data.@this> { new("ms", 1) }
            }
        };

        Func<Task<global::App.Data.@this>> throwingInner = async () =>
        {
            await Task.Delay(500); // long enough for the 1ms timeout to fire
            throw new OperationCanceledException();
        };

        var result = await modifiers.RunAsync(throwingInner, Ctx);

        await Assert.That(result.Success).IsFalse();
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
            Parameters = new List<global::App.Data.@this> { new("ms", 5000) },
            Modifiers = new ActionModifiers
            {
                TimeoutModifier(50),
                new PrAction
                {
                    Module = "error", ActionName = "handle",
                    Parameters = new List<global::App.Data.@this> { new("ignoreError", true) }
                }
            }
        };

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
    }
}
