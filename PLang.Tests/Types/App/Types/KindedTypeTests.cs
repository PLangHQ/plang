namespace PLang.Tests.App.Types;

// A kinded type is found, not made: the type answers it (Kinded), made once by the list that made the type; a type no
// list made answers one of its own, as an operator's birth does.
public class KindedTypeTests
{
    // a list born with a bare type (no list made it) still counts
    [Test]
    public async Task AListOfABareType_Counts()
    {
        var list = new global::app.type.item.list.@this(new global::app.type.@this("list"));

        await Assert.That(list.Count.ToString()).IsEqualTo("0");
    }

    // {number, int} is one object, asked through the type in a root app and in its sub app
    [Test]
    public async Task AKindedType_IsOneObject_InARootAndItsSubApp()
    {
        await using var root = new global::app.@this("/kinded").Testing();
        await using var sub = new global::app.@this("/kinded/sub", root.actor.list.User).Testing();
        var number = root.type.list[typeof(global::app.type.item.number.@this)];

        var kinded = number.Kinded("int");

        await Assert.That(number.Kinded("int")).IsSameReferenceAs(kinded);
        await Assert.That(sub.type.list[typeof(global::app.type.item.number.@this)].Kinded("int")).IsSameReferenceAs(kinded);
        await Assert.That(new global::app.type.item.number.@this(number, 5).Type).IsSameReferenceAs(kinded);
        await Assert.That(number.Kinded(null)).IsSameReferenceAs(number);
    }
}
