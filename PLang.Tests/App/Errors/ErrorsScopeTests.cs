using app.error;

namespace PLang.Tests.App.Errors;

public class ErrorsScopeTests
{
    [Test]
    public async Task Error_NullOutsideAnyPushScope()
    {
        await using var app = new global::app.@this("/test");
        var errors = app.Error;
        await Assert.That(errors.Error).IsNull();
    }

    [Test]
    public async Task Push_SetsErrorToPushedValue()
    {
        await using var app = new global::app.@this("/test");
        var errors = app.Error;
        var err = new Error("Boom");
        using (errors.Push(err))
        {
            await Assert.That(errors.Error).IsEqualTo(err);
        }
    }

    [Test]
    public async Task Push_ReturnsDisposable_RestoresPreviousOnDispose()
    {
        await using var app = new global::app.@this("/test");
        var errors = app.Error;
        var err = new Error("Boom");
        using (errors.Push(err)) { }
        await Assert.That(errors.Error).IsNull();
    }

    [Test]
    public async Task Push_NestedScopes_LifoRestore()
    {
        await using var app = new global::app.@this("/test");
        var errors = app.Error;
        var a = new Error("A");
        var b = new Error("B");
        using (errors.Push(a))
        {
            await Assert.That(errors.Error).IsEqualTo(a);
            using (errors.Push(b))
            {
                await Assert.That(errors.Error).IsEqualTo(b);
            }
            await Assert.That(errors.Error).IsEqualTo(a);
        }
        await Assert.That(errors.Error).IsNull();
    }

    [Test]
    public async Task Trail_AccumulatesEveryPushedError()
    {
        await using var app = new global::app.@this("/test");
        var errors = app.Error;
        var a = new Error("A");
        var b = new Error("B");
        using (errors.Push(a)) { using (errors.Push(b)) { } }
        await Assert.That(errors.Trail.Count).IsEqualTo(2);
        await Assert.That(errors.Trail[0]).IsEqualTo(a);
        await Assert.That(errors.Trail[1]).IsEqualTo(b);
    }

    [Test]
    public async Task Error_FlowsAcrossAwait_ViaAsyncLocal()
    {
        await using var app = new global::app.@this("/test");
        var errors = app.Error;
        var err = new Error("X");
        using (errors.Push(err))
        {
            await Task.Yield();
            await Assert.That(errors.Error).IsEqualTo(err);
        }
    }

    [Test]
    public async Task Error_DoesNotLeakAcrossParallelBranches()
    {
        await using var app = new global::app.@this("/test");
        var errors = app.Error;
        var a = new Error("A");
        var b = new Error("B");
        IError? aSeen = null, bSeen = null;

        async Task BranchA() { using (errors.Push(a)) { await Task.Yield(); aSeen = errors.Error; } }
        async Task BranchB() { using (errors.Push(b)) { await Task.Yield(); bSeen = errors.Error; } }

        await Task.WhenAll(BranchA(), BranchB());
        // Each branch saw its own pushed error.
        await Assert.That(aSeen).IsEqualTo(a);
        await Assert.That(bSeen).IsEqualTo(b);
    }
}
