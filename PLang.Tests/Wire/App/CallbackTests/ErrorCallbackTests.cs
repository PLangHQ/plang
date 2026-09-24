namespace PLang.Tests.App.CallbackTests;

/// <summary>
/// An error reaches its App through its own Context. An error raised inside a run (born with a
/// context) answers a callback snapshot; an error with no context has nothing to resume — its
/// callback is a named NoCallback error, never a throw.
/// </summary>
public class ErrorCallbackTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = TestApp.Create("/tmp/errcb-" + System.Guid.NewGuid().ToString("N")[..8]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Callback_ErrorWithNoContext_IsNoCallback()
    {
        var error = new global::app.error.Error("boom", "Boom");

        var callback = error.Callback;

        await Assert.That(callback.Success).IsFalse();
        await Assert.That(callback.Error?.Key).IsEqualTo("NoCallback");
    }

    [Test]
    public async Task Callback_ErrorWithContext_AnswersASnapshot()
    {
        var error = new global::app.error.Error("boom", _app.User.Context, "Boom");

        var callback = error.Callback;

        await callback.IsSuccess();
        await Assert.That(callback.Snapshot).IsNotNull();
        await Assert.That(ReferenceEquals(error.Callback, callback)).IsTrue();
    }
}
