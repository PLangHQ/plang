using Bool = global::app.type.item.@bool.@this;

using PLang.Tests.App.Fixtures;

namespace PLang.Tests.App.ScalarsAsNative;

// bool.@this — the truthiness primitive. Wraps a raw bool; this is where the
// IBooleanResolvable turtles stop. Equality-only (Order throws). OwnedClrTypes = bool.
public class BoolWrapperTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.Session.App("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task Bool_WrapsRawBool_AsBooleanAsyncBottomsOutAtValue()
    {
        await Assert.That(await app.actor.list.User.Context.Bool(true).AsBooleanAsync(app.actor.list.User.Context)).IsTrue();
        await Assert.That(await app.actor.list.User.Context.Bool(false).AsBooleanAsync(app.actor.list.User.Context)).IsFalse();
        await Assert.That(app.actor.list.User.Context.Bool(true).IsTruthy()).IsTrue();
        await Assert.That(app.actor.list.User.Context.Bool(false).IsTruthy()).IsFalse();
    }

    [Test]
    public async Task Bool_Equality_TrueEqualsTrueAndHashEqual()
    {
        await Assert.That(app.actor.list.User.Context.Bool(true).Equals(app.actor.list.User.Context.Bool(true))).IsTrue();
        await Assert.That(app.actor.list.User.Context.Bool(true).Equals(app.actor.list.User.Context.Bool(false))).IsFalse();
        await Assert.That(app.actor.list.User.Context.Bool(true).GetHashCode()).IsEqualTo(app.actor.list.User.Context.Bool(true).GetHashCode());
    }

    [Test]
    public async Task Bool_BareSerialize_TrueFalseOnApplicationJson()
    {
        // Bare lowercase true/false form.
        await Assert.That(app.actor.list.User.Context.Bool(true).ToString()).IsEqualTo("true");
        await Assert.That(app.actor.list.User.Context.Bool(false).ToString()).IsEqualTo("false");
    }

    [Test]
    public async Task Bool_OwnsClrBool_RegisteredInOwnedClrTypes()
    {
        await Assert.That(Bool.OwnedClrTypes.Any(o => o.Clr == typeof(bool))).IsTrue();
    }

    [Test]
    public async Task Bool_NotOrderable_OrderThrows()
    {
        // bool is equality-only: unequal bools answer NotEqual (never an order),
        // so the ordering boundary errors.
        await Assert.That(await app.actor.list.User.Context.Bool(true).Compare(app.actor.list.User.Context.Bool(false), app.actor.list.User.Context)).IsEqualTo(global::app.data.Comparison.NotEqual);
        await Assert.That(() => CompareTestOps.OrdD(new Data("", app.actor.list.User.Context.Bool(true), context: app.actor.list.User.Context), new Data("", app.actor.list.User.Context.Bool(false), context: app.actor.list.User.Context)))
            .Throws<global::app.data.IncomparableException>();
    }
}
