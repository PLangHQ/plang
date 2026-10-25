namespace PLang.Tests.App.Types;

/// <summary>The code's facts are read once per code: an app an actor made reads no class at its birth — its type list is
/// a layer over its caller's, holding nothing of its own until it loads code — while an app no actor made reads its own.</summary>
public class CodeFactsTests
{
    [Test]
    public async Task AChildsTypeList_HoldsNothingOfItsOwn_AtBirth()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var child = new global::app.@this("/parent/child", parent.actor.list.User).Testing();

        await Assert.That(child.type.list.HasOwn).IsFalse().Because("a sub app scans no class: its types are its parent's");
        await Assert.That(child.type.list["text"]).IsSameReferenceAs(parent.type.list["text"]);
        await Assert.That(child.type.list.CountRaw).IsEqualTo(parent.type.list.CountRaw);
    }

    [Test]
    public async Task AnAppNoActorMade_ReadsItsOwnFacts()
    {
        await using var one = new global::app.@this("/one").Testing();
        await using var other = new global::app.@this("/other").Testing();

        await Assert.That(other.type.list.code).IsNotSameReferenceAs(one.type.list.code);
        await Assert.That(other.type.list["text"]).IsNotSameReferenceAs(one.type.list["text"]);
    }
}
