namespace PLang.Tests.App.CollectionsAreData;

// A list's reads share one snapshot of its rows until the next write: a read after a write sees it, a walk
// already under way keeps the rows it started with, and reads between writes copy nothing.
public class ListCopyOnWriteTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    private async Task<List<string>> Texts(global::app.type.item.list.@this list)
    {
        var texts = new List<string>();
        foreach (var element in list.Items(Ctx)) texts.Add((await element.Value())?.ToString() ?? "");
        return texts;
    }

    [Test] public async Task AReadAfterAWrite_SeesIt()
    {
        var list = new global::app.type.item.list.@this();
        list.Add((global::app.type.item.text.@this)"a");
        await Assert.That(await Texts(list)).IsEquivalentTo(new[] { "a" });
        list.Add((global::app.type.item.text.@this)"b");
        await Assert.That(await Texts(list)).IsEquivalentTo(new[] { "a", "b" });
        list.RemoveAt(0);
        await Assert.That(await Texts(list)).IsEquivalentTo(new[] { "b" });
    }

    [Test] public async Task AWalkUnderWay_KeepsTheRowsItStartedWith()
    {
        var list = new global::app.type.item.list.@this();
        list.Add((global::app.type.item.text.@this)"a");
        list.Add((global::app.type.item.text.@this)"b");
        var seen = new List<string>();
        foreach (var element in list.Items(Ctx))
        {
            seen.Add((await element.Value())?.ToString() ?? "");
            list.Add((global::app.type.item.text.@this)"added while walking");
        }
        await Assert.That(seen).IsEquivalentTo(new[] { "a", "b" });
        await Assert.That(list.CountRaw).IsEqualTo(4);
    }
}
