namespace PLang.Tests.App.VariablesTests;

// The variables a snapshot captures are keyed as the store keys them: one Data held under two names (a step's result
// under its own name and another) is captured, and restored, under both.
public class VariableSnapshotTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    private readonly global::app.@this dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();

    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        await dst.DisposeAsync();
    }

    [Test]
    public async Task AValueUnderTwoNames_IsCapturedAndRestoredUnderBoth()
    {
        var context = app.actor.list.User.Context;
        var result = new global::app.data.@this("answer", "42", context: context);
        await context.Variable.Set("answer", result);
        await context.Variable.Set("last", result);

        var snap = new Snapshot(context);
        ((ISnapshot)context.Variable).Store(snap, context);
        var into = dst.actor.list.User.Context;
        await ((ISnapshot)into.Variable).Start(snap, into);

        await Assert.That((await (await into.Variable.Get("answer")).Value())?.ToString()).IsEqualTo("42");
        await Assert.That((await (await into.Variable.Get("last")).Value())?.ToString()).IsEqualTo("42");
    }
}
