using static PLang.Tests.TestAction;

namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// Tests for the on.timeout clause: each attempt of the action before it gets a deadline, After from its start.
/// </summary>
public class TimeoutAfterTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/app");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // An on.timeout clause: a deadline of ms per attempt.
    private static PrAction TimeoutModifier(int ms)
        => global::PLang.Tests.Shared.Make.Action("on", "timeout", ("After", System.TimeSpan.FromMilliseconds(ms)));

    [Test]
    public async Task After_ActionCompletesBefore_PassesThroughResult()
    {
        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("variable"),
            Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%fast%", new global::app.type.@this("variable"), context: Ctx), new("value", "done", context: Ctx)
            })
        }, TimeoutModifier(5000));

        var result = await action.Start(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("fast"))).IsEqualTo("done");
    }

    [Test]
    public async Task After_ActionExceedsTimeout_Returns408Error()
    {
        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("timer"),
            Name = "sleep",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this> { new("ms", 5000, context: Ctx) })
        }, TimeoutModifier(50));

        var result = await action.Start(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(408);
    }

    [Test]
    public async Task After_CancellationTokenPropagatedToAction()
    {
        // Token did propagate: sleep was cut short well before its 10s target
        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("timer"),
            Name = "sleep",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this> { new("ms", 10_000, context: Ctx) })
        }, TimeoutModifier(30));

        var start = DateTimeOffset.UtcNow;
        var result = await action.Start(Ctx);
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

        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("timer"),
            Name = "sleep",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this> { new("ms", 10_000, context: Ctx) })
        }, TimeoutModifier(5000));

        await Assert.That(async () => await action.Start(Ctx))
            .Throws<OperationCanceledException>();

        Ctx.PopCancellation();
    }

    [Test]
    public async Task After_ZeroMsTimeout_ImmediateTimeout()
    {
        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("timer"),
            Name = "sleep",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this> { new("ms", 1000, context: Ctx) })
        }, TimeoutModifier(0));

        var result = await action.Start(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
    }

    [Test]
    public async Task After_WithAnOnError_TimeoutIsAnIgnorableError()
    {
        // timer.sleep(5000); on.error(ignore); on.timeout(50): the sleep exceeds the deadline, so the attempt's
        // verdict is a 408 — and the error outcome, after the attempt, ignores it like any other error → Ok.
        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("timer"),
            Name = "sleep",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this> { new("ms", 5000, context: Ctx) })
        }, global::PLang.Tests.Shared.Make.Action("on", "error", ("IgnoreError", true)),
                TimeoutModifier(50));

        var result = await action.Start(Ctx);

        await result.IsSuccess();
    }

    [Test]
    public async Task After_EachRetry_IsAFreshAttempt_WithAFreshDeadline()
    {
        // timer.sleep(2000); on.timeout(100); on.error(RetryCount=2): three attempts, each cut at its own 100ms.
        // A deadline shared across the attempts would be spent by the first, and the retries would fail at once.
        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("timer"),
            Name = "sleep",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this> { new("ms", 2000, context: Ctx) })
        }, TimeoutModifier(100), global::PLang.Tests.Shared.Make.Action("on", "error", ("RetryCount", 2)));

        var start = DateTimeOffset.UtcNow;
        var result = await action.Start(Ctx);
        var elapsed = DateTimeOffset.UtcNow - start;

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
        // each attempt ran to its own 100ms deadline (≥ ~300ms), and none ran its full 2s sleep (3 × 2s uncut)
        await Assert.That(elapsed.TotalMilliseconds).IsGreaterThanOrEqualTo(280);
        await Assert.That(elapsed.TotalMilliseconds).IsLessThan(4000);
    }
}
