namespace PLang.Tests.App.Types;

// A read through the type readers finds the types it holds; it makes none to ask the list.
public class TypeReadTests
{
    // a read through the readers (a number, a list, a dict, a text, a duration) finds each type, makes none
    [Test]
    public async Task ARead_FindsItsTypes()
    {
        await using var app = new global::app.@this("/kinded", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var json = app.type.list.Kind("item", "json");
        var bytes = System.Text.Encoding.UTF8.GetBytes("""{"n":5,"l":[1,2,3],"d":{"t":"x"},"s":"hello"}""");
        async Task Read()
        {
            var read = await json.Decode(bytes, context);
            var dict = (await read.Value<global::app.type.item.dict.@this>())!;
            foreach (var row in dict.Rows(context)) await row.Value();
        }
        await Read();

        var from = System.GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20; i++) await Read();
        var born = (System.GC.GetAllocatedBytesForCurrentThread() - from) / 20;

        // 9,936 bytes on this machine; 24,608 with a type made to ask the list for each
        await Assert.That(born).IsLessThan(10_300L).Because("a read finds its types through the list, it makes none to ask");
    }
}
