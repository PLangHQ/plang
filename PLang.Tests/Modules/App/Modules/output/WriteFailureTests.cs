namespace PLang.Tests.App.actions.output;

/// <summary>
/// A value that can't be read when it is written out (a %variable% not set) is the write's failure — the action
/// fails with its key, so the step's on error takes it — never written as if it were the content.
/// </summary>
public class WriteFailureTests
{
    private readonly global::app.@this _app = new global::app.@this("/test").Testing();

    [After(Test)]
    public async Task Dispose() => await _app.DisposeAsync();

    [Test]
    public async Task WritingAPropertyTheValueDoesNotHave_FailsTheWrite()
    {
        var ctx = _app.actor.list.User.Context;
        await ctx.Variable.Set("event", new Dictionary<string, object?> { ["mouse"] = 1 });

        var written = await global::PLang.Tests.Shared.Make.Action(ctx, "output", "write", ("Data", "%event.guest%")).Start(ctx);

        await written.IsFailure();
        await Assert.That(written.Error!.Key).IsEqualTo("VariableNotFound");
    }
}
