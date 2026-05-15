using global::app.Errors;

namespace PLang.Tests.App.CallStackTests;

public class FlagsDiffAutoFlipTests
{
    [Test]
    public async Task FlagsDiff_AutoFlipsOn_DuringErrorProcessing()
    {
        var app = new global::app.@this("/test");
        await Assert.That(app.callstack.Flags.Diff).IsFalse();

        using (app.errors.Push(new ServiceError("boom", "TestErr", 400)))
        {
            await Assert.That(app.callstack.Flags.Diff).IsTrue();
        }
    }

    [Test]
    public async Task FlagsDiff_RestoredToPriorState_AfterErrorPathCompletes()
    {
        var app = new global::app.@this("/test");
        // Off baseline.
        await Assert.That(app.callstack.Flags.Diff).IsFalse();

        using (app.errors.Push(new ServiceError("boom", "TestErr", 400))) { /* scoped */ }
        await Assert.That(app.callstack.Flags.Diff).IsFalse();

        // Now with Diff already on — Push should not turn it off afterwards.
        app.callstack.Flags = app.callstack.Flags with { Diff = true };
        using (app.errors.Push(new ServiceError("boom2", "TestErr", 400))) { /* scoped */ }
        await Assert.That(app.callstack.Flags.Diff).IsTrue();
    }
}
