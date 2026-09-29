using app;
using app.actor.context;
using app.type.item.variable;
using app.type.item.path;

namespace PLang.Tests.App.Modules.condition;

/// <summary>
/// A condition's body is its child steps; an error in the body is the step's answer — never swallowed by
/// the condition that started it.
/// </summary>
public class IfErrorOrchestrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;

    public IfErrorOrchestrationTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_if_err_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new global::app.@this(_tempDir).Testing();
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    [Test]
    public async Task If_BodyActionReturnsError_PropagatesThroughStep()
    {
        var ctx = _app.actor.list.User.Context;
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(ctx, "IfCallMissing",
            Make.Step("if true, call DoesNotExist",
                ctx.Action("condition.if(Left=true, Operator=\"==\", Right=true) { goal.call(Name=\"DoesNotExist\") }"))));
        var step = goal.Step[0];

        var result = await step.Start(ctx);

        // The 404 surfaces; pin the error identity so an unrelated error leaking through wouldn't pass.
        await result.IsFailure();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
        await Assert.That(result.Error!.Key).IsEqualTo("GoalNotFound");
    }
}
