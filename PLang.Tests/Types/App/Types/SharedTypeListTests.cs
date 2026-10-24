namespace PLang.Tests.App.Types;

/// <summary>A sub app keeps its parent's type list whole under a layer of its own: a type it reaches is the parent's
/// own object, what it adds is its own alone, a kind on a type its parent holds is refused naming the type, and what the
/// parent loads after making it is the sub app's too.</summary>
public class SharedTypeListTests
{
    /// <summary>A type a test adds to an app.</summary>
    [global::app.Attributes.PlangType("sharedprobe")]
    public sealed class SharedProbe : global::app.type.item.@this
    {
        public SharedProbe(global::app.type.@this type) : base(type) { }
    }

    /// <summary>A kind a test adds to a type: born holding the type it is a kind of.</summary>
    private sealed class ProbeKind(global::app.type.@this owner) : global::app.type.kind.@this("webpx", owner);

    [Test]
    public async Task AChildsType_IsItsParentsType()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var child = new global::app.@this("/child", parent.actor.list.User).Testing();

        await Assert.That(child.type.list).IsNotSameReferenceAs(parent.type.list).Because("a sub app's list is a layer of its own");
        await Assert.That(child.type.list["text"]).IsSameReferenceAs(parent.type.list["text"]);
        await Assert.That(child.type.list[typeof(global::app.type.item.text.@this)]).IsSameReferenceAs(parent.type.list["text"]);
    }

    [Test]
    public async Task ATypeASubAppAdds_IsOnlyItsOwn()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var sibling = new global::app.@this("/sibling", parent.actor.list.User).Testing();
        var child = new global::app.@this("/child", parent.actor.list.User).Testing();

        var added = child.type.list.Add(typeof(SharedProbe), child.actor.list.System.Context);

        await added.IsSuccess();
        await Assert.That(child.type.list.Contains("sharedprobe")).IsTrue();
        await Assert.That(parent.type.list.Contains("sharedprobe")).IsFalse().Because("the parent never has what a sub app loads");
        await Assert.That(sibling.type.list.Contains("sharedprobe")).IsFalse().Because("nor does a sibling");
        await child.DisposeAsync();
        await Assert.That(parent.type.list.Contains("sharedprobe")).IsFalse();
    }

    [Test]
    public async Task AKindOnAParentsType_IsRefused_NamingIt()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var child = new global::app.@this("/child", parent.actor.list.User).Testing();
        var image = child.type.list["image"];

        var refused = await Assert.ThrowsAsync<global::app.error.AppException>(() =>
        {
            child.type.list.Add(new ProbeKind(image));
            return Task.CompletedTask;
        });

        await Assert.That(refused!.Error.Key).IsEqualTo("TypesShared");
        await Assert.That(image.kind["webpx"]).IsNull().Because("the parent's entry holds nothing a sub app tried to add");
    }

    [Test]
    public async Task AParentLoadingAfterMakingASubApp_IsTheSubAppsToo()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var child = new global::app.@this("/child", parent.actor.list.User).Testing();

        var added = parent.type.list.Add(typeof(SharedProbe), parent.actor.list.System.Context);

        await added.IsSuccess();
        await Assert.That(child.type.list.Contains("sharedprobe")).IsTrue().Because("a sub app reads its parent's list live");
        await Assert.That(child.type.list["sharedprobe"]).IsSameReferenceAs(parent.type.list["sharedprobe"]);
    }
}
