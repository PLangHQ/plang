namespace PLang.Tests.App.CallbackTests;

/// <summary>
/// An error reaches its App through its own Context. An error raised inside a run (born with a
/// context) answers a callback snapshot; an error with no context has nothing to resume — its
/// callback is a named NoCallback error, never a throw.
/// </summary>
public class ErrorCallbackTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/errcb-" + System.Guid.NewGuid().ToString("N")[..8], global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Callback_ErrorWithNoContext_IsNoCallback()
    {
        var error = new global::app.error.@this("boom", "Boom");

        // an error not raised inside a run holds none to resume: it answers so, to its asker
        var refused = await error.Callback(_app.actor.list.User.Context);
        await refused.IsFailure();
        await Assert.That(refused.Error!.Key).IsEqualTo("NoCallback");
    }

    [Test]
    public async Task Callback_ErrorWithContext_AnswersASnapshot()
    {
        var error = new global::app.error.@this("boom", _app.actor.list.User.Context, "Boom");

        var callback = await error.Callback(_app.actor.list.User.Context);

        await callback.IsSuccess();
        await Assert.That(callback.Snapshot).IsNotNull();
        await Assert.That(ReferenceEquals(await error.Callback(_app.actor.list.User.Context), callback)).IsTrue();
    }

    // The callback stands where the error happened: its call is the error's, though those frames have ended by the time
    // a handler asks — the snapshot holds the error's goal's frame, not the empty stack the handler stands in.
    [Test]
    public async Task Callback_StandsAtTheErrorsCall_AfterItsFramesEnded()
    {
        var context = _app.actor.list.User.Context;
        var goal = new Goal(_app.type.list) { Name = "Where", Path = global::app.type.item.path.@this.Resolve("/Where.goal", context) };
        var step = new Step(_app.type.list) { Goal = goal, Index = 0, Text = "step" };
        step.Code.Add((await context.Action("variable.set(Name=%x%, Value=1)")).In(step));
        goal.Step.Add(step);
        _app.goal.list.Add(goal);
        var error = new global::app.error.@this("boom", context, "Boom");
        await using (context.call.Push(goal, await context.call.Setting()))
        await using (context.call.Push(step, await context.call.Setting()))
        await using (var frame = context.call.Push(step.Code[0], await context.call.Setting()))
            frame.Record(error, context);
        await Assert.That(context.call.Current).IsNull();
        await Assert.That(error.call).IsNotNull();

        var callback = await error.Callback(context);

        await callback.IsSuccess();
        var text = System.Text.RegularExpressions.Regex.Replace(await callback.Snapshot!.Text(context), @"\s", "");
        await Assert.That(text).Contains("\"goal\":{\"name\":\"Where\"");
    }

    // An error built without a context takes the handler's when it is returned through
    // Context.Error — the first run it meets — so its callback answers a snapshot.
    [Test]
    public async Task ServiceError_ReturnedViaContextError_HasTheHandlersContext_AndACallback()
    {
        var error = new global::app.error.ServiceError("boom", "Boom", 500);

        var result = _app.actor.list.User.Context.Error(error);

        await Assert.That(ReferenceEquals(result.Error!.Context, _app.actor.list.User.Context)).IsTrue();
        var callback = await error.Callback(_app.actor.list.User.Context);
        await callback.IsSuccess();
        await Assert.That(callback.Snapshot).IsNotNull();
    }

    // Passed up through a second context, the error keeps where it happened.
    [Test]
    public async Task Error_PassedUpThroughASecondContext_KeepsTheFirst()
    {
        var error = new global::app.error.ServiceError("boom", "Boom", 500);

        _app.actor.list.User.Context.Error(error);
        _app.actor.list.System.Context.Error(error);

        await Assert.That(ReferenceEquals(error.Context, _app.actor.list.User.Context)).IsTrue();
    }

    // A value's Fail stamps the Data's own context.
    [Test]
    public async Task Fail_StampsTheDatasContext()
    {
        var error = new global::app.error.@this("declined", "Declined");
        var data = new global::app.data.@this("x", context: _app.actor.list.User.Context);

        data.Fail(error);

        await Assert.That(ReferenceEquals(error.Context, _app.actor.list.User.Context)).IsTrue();
    }
}
