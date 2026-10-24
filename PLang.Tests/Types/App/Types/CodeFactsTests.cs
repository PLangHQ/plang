namespace PLang.Tests.App.Types;

/// <summary>The code's facts are read once per code: an app an actor made has its own type list, born from its
/// caller's list's facts — the same facts, not read again.</summary>
public class CodeFactsTests
{
    [Test]
    public async Task AChildsTypeList_HoldsItsParentsFacts()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var child = new global::app.@this("/child", parent.actor.list.User).Testing();

        await Assert.That(child.type.list).IsNotSameReferenceAs(parent.type.list).Because("each child still makes its own list");
        await Assert.That(child.type.list.code).IsSameReferenceAs(parent.type.list.code)
            .Because("the child's list is born from its parent's list's facts");
    }

    [Test]
    public async Task AnAppNoActorMade_ReadsItsOwnFacts()
    {
        await using var one = new global::app.@this("/one").Testing();
        await using var other = new global::app.@this("/other").Testing();

        await Assert.That(other.type.list.code).IsNotSameReferenceAs(one.type.list.code);
    }
}
