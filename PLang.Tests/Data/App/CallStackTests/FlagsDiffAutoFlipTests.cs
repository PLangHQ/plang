using app.error;

namespace PLang.Tests.App.CallStackTests;

public class FlagsDiffAutoFlipTests
{
    [Test]
    public async Task FlagsDiff_AutoFlipsOn_DuringErrorProcessing()
    {
        var app = global::PLang.Tests.TestApp.Create("/test");
        await Assert.That(app.User.CallStack.Diff.Value).IsFalse();

        using (app.Error.Push(new ServiceError("boom", "TestErr", 400), app.User.Context))
        {
            await Assert.That(app.User.CallStack.Diff.Value).IsTrue();
        }
    }

    [Test]
    public async Task FlagsDiff_RestoredToPriorState_AfterErrorPathCompletes()
    {
        var app = global::PLang.Tests.TestApp.Create("/test");
        // Off baseline.
        await Assert.That(app.User.CallStack.Diff.Value).IsFalse();

        using (app.Error.Push(new ServiceError("boom", "TestErr", 400), app.User.Context)) { /* scoped */ }
        await Assert.That(app.User.CallStack.Diff.Value).IsFalse();

        // Now with Diff already on — Push should not turn it off afterwards.
        app.User.CallStack.Diff = true;
        using (app.Error.Push(new ServiceError("boom2", "TestErr", 400), app.User.Context)) { /* scoped */ }
        await Assert.That(app.User.CallStack.Diff.Value).IsTrue();
    }
}
