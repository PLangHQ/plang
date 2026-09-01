namespace PLang.Tests.App.CallStackTests;

/// <summary>
/// The diff-capture scope. Error handling opens one so handler-time mutations land on the
/// CallStack's diff stream and Variables.SnapshotAt can project back to throw-time state;
/// the scope itself belongs to the CallStack, which owns the flag, the stream and the
/// subscriptions.
/// </summary>
public class FlagsDiffAutoFlipTests
{
    [Test]
    public async Task Diff_IsOn_InsideADiffScope()
    {
        var app = global::PLang.Tests.TestApp.Create("/test");
        await Assert.That(app.User.CallStack.Diff.Value).IsFalse();

        using (app.User.CallStack.DiffScope(app.User.Context.Variable))
        {
            await Assert.That(app.User.CallStack.Diff.Value).IsTrue();
        }
    }

    [Test]
    public async Task Diff_RestoredToPriorState_WhenTheScopeCloses()
    {
        var app = global::PLang.Tests.TestApp.Create("/test");
        // Off baseline.
        await Assert.That(app.User.CallStack.Diff.Value).IsFalse();

        using (app.User.CallStack.DiffScope(app.User.Context.Variable)) { /* scoped */ }
        await Assert.That(app.User.CallStack.Diff.Value).IsFalse();

        // Now with Diff already on — the scope must not turn it off afterwards.
        app.User.CallStack.Diff = true;
        using (app.User.CallStack.DiffScope(app.User.Context.Variable)) { /* scoped */ }
        await Assert.That(app.User.CallStack.Diff.Value).IsTrue();
    }
}
