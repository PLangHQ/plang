using app.actor.context;
using app;

namespace PLang.Tests.App.actions.error;

/// <summary>
/// error.throw through the real path — Make.Goal -> RealGoalLoad.ViaChannel ->
/// RunGoalAsync — asserting on the failure Data a PLang author observes (raised error's
/// message / key / status-code / rendered Format). Replaces the hand-built
/// `new Throw{...}.Run()` units for the language-observable cases. The internal
/// Error.Data list-normalization shape and raw-error re-raise (no clean language seed)
/// stay as named C# floor in ThrowTests.
/// </summary>
public class ErrorThrowGoalRunTests
{
    static async Task<(global::app.@this engine, global::app.actor.context.@this ctx, global::app.data.@this result)>
        Run(global::app.goal.@this spec)
    {
        var engine = TestApp.Create("/app");
        var goal = await RealGoalLoad.ViaChannel(engine, spec);
        engine.Goal.Add(goal);
        var ctx = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, ctx);
        return (engine, ctx, result);
    }

    static global::app.goal.@this Throw(params (string name, object? value)[] parameters)
        => Make.Goal("T", Make.Step("throw", Make.Action("error", "throw", parameters)));

    [Test]
    public async Task Throw_ReturnsFailure_WithMessage()
    {
        var (engine, _, result) = await Run(Throw(("Message", "Something went wrong"), ("StatusCode", 500)));
        await using var _e = engine;
        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Message).IsEqualTo("Something went wrong");
    }

    [Test]
    public async Task Throw_UsesCustomKeyAndStatus()
    {
        var (engine, _, result) = await Run(Throw(("Message", "Not found"), ("StatusCode", 404), ("Key", "NotFound")));
        await using var _e = engine;
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Throw_DefaultsStatusCode500()
    {
        var (engine, _, result) = await Run(Throw(("Message", "Server error")));
        await using var _e = engine;
        await result.IsFailure();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Throw_DataRendersFullyInFormat()
    {
        // The attached value shows in the error display (Format), not as a type name.
        var (engine, _, result) = await Run(Throw(("Message", "checkout failed"), ("Data", "order-123")));
        await using var _e = engine;
        var formatted = result.Error!.Format();
        await Assert.That(formatted).Contains("checkout failed");
        await Assert.That(formatted).Contains("order-123");
    }
}
