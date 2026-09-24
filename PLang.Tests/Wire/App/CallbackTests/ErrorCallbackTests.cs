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

    // An error built without a context takes the handler's when it is returned through
    // Context.Error — the first run it meets — so its callback answers a snapshot.
    [Test]
    public async Task ServiceError_ReturnedViaContextError_HasTheHandlersContext_AndACallback()
    {
        var error = new global::app.error.ServiceError("boom", "Boom", 500);

        var result = _app.User.Context.Error(error);

        await Assert.That(ReferenceEquals(result.Error!.Context, _app.User.Context)).IsTrue();
        await error.Callback.IsSuccess();
        await Assert.That(error.Callback.Snapshot).IsNotNull();
    }

    // Passed up through a second context, the error keeps where it happened.
    [Test]
    public async Task Error_PassedUpThroughASecondContext_KeepsTheFirst()
    {
        var error = new global::app.error.ServiceError("boom", "Boom", 500);

        _app.User.Context.Error(error);
        _app.System.Context.Error(error);

        await Assert.That(ReferenceEquals(error.Context, _app.User.Context)).IsTrue();
    }

    // A value's Fail stamps the Data's own context.
    [Test]
    public async Task Fail_StampsTheDatasContext()
    {
        var error = new global::app.error.Error("declined", "Declined");
        var data = new global::app.data.@this("x", context: _app.User.Context);

        data.Fail(error);

        await Assert.That(ReferenceEquals(error.Context, _app.User.Context)).IsTrue();
    }
}
