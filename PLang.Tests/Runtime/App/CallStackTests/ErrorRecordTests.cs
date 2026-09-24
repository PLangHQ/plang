using PLang.Tests.Shared;

namespace PLang.Tests.App.CallStackTests;

/// <summary>
/// The frame's Record is the one door every error passes: it stamps the context the error met and,
/// under --debug only, the variables as they were when it happened (variables can hold secrets).
/// </summary>
public class ErrorRecordTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = TestApp.Create("/tmp/errrec-" + System.Guid.NewGuid().ToString("N")[..8]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private async Task<global::app.error.Error> Recorded()
    {
        var context = _app.User.Context;
        await context.Variable.Set("secret", "hunter2");
        var action = TestAction.Create("variable", "set", ("name", "%x%"), ("value", 1));
        var error = new global::app.error.ServiceError("boom", "Boom", 500);
        await using var call = context.CallStack.Push(action, context.Variable);
        call.Record(error, context);
        return error;
    }

    [Test]
    public async Task Record_UnderDebug_CapturesTheVariablesWhole()
    {
        _app.Debug = new global::app.module.action.debug.@this(_app.System.Context);

        var error = await Recorded();

        await Assert.That(error.Variables).IsNotNull();
        await Assert.That(error.Variables!.Held("secret")?.ToString()).IsEqualTo("hunter2");
    }

    [Test]
    public async Task Record_WithoutDebug_CapturesNoVariables()
    {
        var error = await Recorded();

        await Assert.That(error.Variables).IsNull();
    }

    [Test]
    public async Task Record_StampsTheContextItMet()
    {
        var error = await Recorded();

        await Assert.That(ReferenceEquals(error.Context, _app.User.Context)).IsTrue();
    }
}
