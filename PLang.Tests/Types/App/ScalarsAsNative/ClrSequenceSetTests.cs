namespace PLang.Tests.App.ScalarsAsNative;

// A C# sequence a carrier holds, written at an index, answers as the plang list it is — so it writes as a list,
// never as the C# list's own members.
public class ClrSequenceSetTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task ASequenceWrittenAtAnIndex_AnswersAsAList()
    {
        var ctx = app.actor.list.User.Context;
        var host = new System.Collections.Generic.List<object?> { 1L, 2L };

        var set = await new global::app.type.clr.@this(host, ctx).Set("0", true, 5L, ctx);
        var value = await set.Value();

        await Assert.That(value is global::app.type.item.list.@this).IsTrue();
        await Assert.That(host[0]).IsEqualTo(5L);
    }
}
