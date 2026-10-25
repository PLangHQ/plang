namespace PLang.Tests.App.Types;

/// <summary>A run never writes an object its app shares with others: the sub apps a parent makes read values that name
/// a type (a .pr slice "bool" reads to the registered entry), and the entry every one of them shares keeps nothing of
/// any of those reads.</summary>
public class SharedEntryWritesTests
{
    [Test]
    public async Task RequestsThroughOneParent_LeaveNoHistoryOnASharedTypeEntry()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        var types = parent.type.list;
        var entry = types["bool"];

        for (var i = 0; i < 5; i++)
        {
            await using var request = new global::app.@this("/parent/request", parent.actor.list.User).Testing();
            var ctx = request.actor.list.User.Context;
            // a value written as a type's name, read as a .pr slice is: a source the type's reader opens
            var slice = Make.FromRaw("bool", request.type.list["type"], ctx);
            var read = await slice.Value();
            await Assert.That(read).IsSameReferenceAs(entry).Because("a type's name reads to the entry every app shares");
        }

        await Assert.That(entry.history.list.Count).IsEqualTo(0)
            .Because("no request's read is written onto the entry its parent shares");
    }
}
