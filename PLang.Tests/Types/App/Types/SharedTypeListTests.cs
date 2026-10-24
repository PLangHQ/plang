namespace PLang.Tests.App.Types;

/// <summary>An app an actor made takes its parent's type list whole: a type in it is the parent's own object, and the
/// shared list refuses an addition, saying why — an app whose program brings code in is made as an app of its own.</summary>
public class SharedTypeListTests
{
    /// <summary>A type the test tries to add to a shared list.</summary>
    [global::app.Attributes.PlangType("sharedprobe")]
    public sealed class SharedProbe : global::app.type.item.@this
    {
        public SharedProbe(global::app.type.@this type) : base(type) { }
    }

    [Test]
    public async Task AChildsType_IsItsParentsType()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var child = new global::app.@this("/child", parent.actor.list.User).Testing();

        await Assert.That(child.type.list).IsSameReferenceAs(parent.type.list);
        await Assert.That(child.type.list["text"]).IsSameReferenceAs(parent.type.list["text"]);
    }

    [Test]
    public async Task ASharedTypeList_RefusesAnAddition_SayingWhy()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var child = new global::app.@this("/child", parent.actor.list.User).Testing();

        var added = child.type.list.Add(typeof(SharedProbe), child.actor.list.System.Context);

        await Assert.That(added.Error?.Key).IsEqualTo("TypesShared");
        await Assert.That(parent.type.list.Contains("sharedprobe")).IsFalse().Because("nothing a child adds reaches its parent's apps");
    }
}
