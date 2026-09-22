namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// A modifier does work of its own — it resolves its own properties, and it decides. When that work
/// fails, the failure has to land on a frame like any other, or it is invisible: <c>%!error%</c> is
/// the call-stack walk, and a failure on no frame is a failure the language cannot see.
///
/// Today <c>modifier.Wrap</c> pushes nothing — the only <c>Push</c> is <c>action/this.cs</c>, for the
/// action itself — so a modifier's own error is returned out of band and never recorded.
/// </summary>
public class ModifierFrameTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    /// <summary>An action wrapped by something that is not a modifier — the failure belongs to the
    /// modifier slot, not to the work, and the work never runs.</summary>
    private static PrAction WithBrokenModifier()
    {
        var action = new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["variable"], Name = "set",
            Parameter = new List<global::app.data.@this>
            {
                new("name", "%x%", new global::app.type.@this("variable"),
                    context: global::PLang.Tests.TestApp.SharedContext),
                new("value", "v", context: global::PLang.Tests.TestApp.SharedContext)
            }
        };
        action.Modifier.Add(new global::app.goal.step.action.modifier.@this
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["output"], Name = "write",
            Parameter = new List<global::app.data.@this>
            {
                new("data", "not a modifier", context: global::PLang.Tests.TestApp.SharedContext)
            }
        });
        return action;
    }

    /// <summary>The failure is reported — the step does not silently succeed.</summary>
    [Test]
    public async Task ModifierOwnFailure_IsReported()
    {
        var result = await WithBrokenModifier().Run(Ctx);

        await result.IsFailure();
    }

    /// <summary>THE GATE: a modifier's own failure is recorded on a frame, so the walk can see it.
    /// Today nothing pushes for a modifier, so the audit has no record of what went wrong.</summary>
    [Test]
    public async Task ModifierOwnFailure_IsRecordedOnTheCallStack()
    {
        var before = Ctx.CallStack.Audit.Count;

        await WithBrokenModifier().Run(Ctx);

        await Assert.That(Ctx.CallStack.Audit.Count).IsGreaterThan(before);
    }
}
