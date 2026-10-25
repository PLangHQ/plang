namespace PLang.Tests.App.Settings;

/// An app's in-memory store is its own: named by the store, not by the app's id (which a folder's app.pr may give two
/// apps alike), and, until something writes to it, it holds nothing — so a read answers empty without opening SQLite.
public class InMemoryStoreTests
{
    [Test]
    public async Task TwoAppsWithOneId_ShareNoStore()
    {
        await using var one = new global::app.@this("/inmemory/one", global::PLang.Tests.Parent.Actor).Testing();
        await using var two = new global::app.@this("/inmemory/two", global::PLang.Tests.Parent.Actor).Testing();
        // as a folder's app.pr gives two apps of it the same id
        two.Id = one.Id;
        var text = one.type.list[typeof(global::app.type.item.text.@this)];

        await (await one.store.Set("probe", "key", new global::app.data.@this("key", new global::app.type.item.text.@this("one's", text), context: one.actor.list.System.Context))).IsSuccess();
        var read = await two.store.Get<global::app.type.item.text.@this>("probe", "key");

        await Assert.That(await read.Value()).IsNull().Because("the second app's store is its own");
    }

    [Test]
    public async Task ABlankStoresRead_OpensNoDatabase()
    {
        var context = global::PLang.Tests.Parent.Actor.Context;
        static long Bytes(System.Func<System.Threading.Tasks.Task> read)
        {
            read().GetAwaiter().GetResult();
            var from = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 20; i++) read().GetAwaiter().GetResult();
            return (System.GC.GetAllocatedBytesForCurrentThread() - from) / 20;
        }

        var born = Bytes(async () =>
        {
            using var store = global::app.store.sqlite.@this.InMemory("blank", context);
            await store.Get<global::app.type.item.text.@this>("probe", "key");
        });

        await Assert.That(born).IsLessThan(5_000L).Because("a read of a store nothing wrote opens no database");
    }
}
