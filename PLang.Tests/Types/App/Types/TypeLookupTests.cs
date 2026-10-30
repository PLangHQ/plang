namespace PLang.Tests.App.Types;

// A lookup by class names the class once: a second lookup is the type kept, answered at once.
public class TypeLookupTests
{
    // the bytes one lookup of `class` allocates, the first paid
    private static long Bytes(global::app.type.list.@this types, System.Type @class)
    {
        _ = types[@class];
        var from = System.GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) _ = types[@class];
        return (System.GC.GetAllocatedBytesForCurrentThread() - from) / 100;
    }

    // a kind of a family (a variable's code, a list node; a hop, a class no type of its own names) in a sub app: the
    // second lookup allocates nothing and answers the same type
    [Test] public Task ASubAppsSecondLookupOfAListNode_AllocatesNothing_AndAnswersTheSameType()
        => Second(typeof(global::app.type.item.variable.code.@this));

    [Test] public Task ASubAppsSecondLookupOfAClassNoTypeNames_AllocatesNothing_AndAnswersTheSameType()
        => Second(typeof(global::app.type.item.variable.code.Variable));

    private static async Task Second(System.Type @class)
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var types = app.type.list;

        var first = types[@class];

        await Assert.That(Bytes(types, @class)).IsLessThanOrEqualTo(8L);
        await Assert.That(types[@class]).IsSameReferenceAs(first);
    }

    // a type that comes in after a lookup is heard: the kept answer is read against the lists' changes
    [Test]
    public async Task ALookup_AfterATypeComesIn_NamesTheClassAgain()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var types = app.type.list;
        var before = types[typeof(Later)];

        types.Add(typeof(Later), app.actor.list.System.Context);

        await Assert.That(before.Name).IsEqualTo("clr");
        await Assert.That(types[typeof(Later)].Name).IsNotEqualTo("clr");
    }

    [global::app.Attributes.PlangType("lookuplater")]
    public sealed class Later : global::app.type.item.@this
    {
        public Later(global::app.type.@this type) : base(type) { }
    }
}
