namespace PLang.Tests.App.Errors;

// New App.Errors.@this — AsyncLocal-flowed current error + run-wide audit.
// Replaces today's Context.Error / vars.Set("!error", ...) registration.
public class ErrorsScopeTests
{
    [Test]
    public async Task Error_NullOutsideAnyPushScope()
    {
        // Fresh app.Errors with no Push: app.Errors.Error is null.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Push_SetsErrorToPushedValue()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Push_ReturnsDisposable_RestoresPreviousOnDispose()
    {
        // using (app.Errors.Push(e)) { } restores Error to its previous value on exit.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Push_NestedScopes_LifoRestore()
    {
        // Push A, Push B, dispose B → Error == A. Dispose A → Error == null.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task All_AccumulatesEveryPushedError()
    {
        // app.Errors.All grows monotonically — every Push appends, never trims.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Error_FlowsAcrossAwait_ViaAsyncLocal()
    {
        // Push, then await some work; inside the awaited continuation, app.Errors.Error is still the pushed value.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Error_DoesNotLeakAcrossParallelBranches()
    {
        // Task.WhenAll(scope1, scope2) where each branch Pushes a different error: branches see only their own.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
