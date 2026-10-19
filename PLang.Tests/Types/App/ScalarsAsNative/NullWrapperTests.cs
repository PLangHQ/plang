using NullV = global::app.type.item.@null.@this;

using PLang.Tests.App.Fixtures;

namespace PLang.Tests.App.ScalarsAsNative;

// null.@this — singleton wrapper for the null *value* (not the absence of a Data).
// Always falsy, null==null true, equality-only, bare `null` on application/json.
// The Data.Null()-stamps-singleton + sort-last-via-Compare integration lands with
// the construction flip (the final coordinated pass), since it touches Data's
// `_value == null` value-switches across Compare/Normalize/ToBoolean.
public class NullWrapperTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private NullV Nul => (NullV)app.actor.list.User.Context.Null().Peek();

    [Test]
    public async Task APresentNull_HoldsTheAppsNullType()
    {
        // a present null is its app's null: born holding the one null type the app made, no shared one
        var held = app.actor.list.User.Context.Null("a").Peek();
        await Assert.That(held is NullV).IsTrue();
        await Assert.That(ReferenceEquals(held.Type, app.type.list["null"])).IsTrue();
        var ctors = typeof(NullV).GetConstructors(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        await Assert.That(ctors.Length).IsEqualTo(0); // no public ctor — a null is made by its type
    }

    [Test]
    public async Task Null_Truthiness_AlwaysFalsy()
    {
        await Assert.That(Nul.IsTruthy()).IsFalse();
        await Assert.That(await Nul.AsBooleanAsync(app.actor.list.User.Context)).IsFalse();
    }

    [Test]
    public async Task Null_Equality_NullEqualsNullNothingElse()
    {
        // null == null true; null == 0/""/false all false (no truthy collapse).
        await Assert.That(Nul.AreEqual(Nul)).IsTrue();
        await Assert.That(Nul.AreEqual(null)).IsTrue();
        await Assert.That(Nul.AreEqual(0L)).IsFalse();
        await Assert.That(Nul.AreEqual("")).IsFalse();
        await Assert.That(Nul.AreEqual(false)).IsFalse();
    }

    [Test]
    public async Task Null_BareSerialize_NullOnApplicationJson()
    {
        // Bare `null` form, not an envelope.
        await Assert.That(Nul.ToString()).IsEqualTo("null");
    }

    [Test]
    public async Task Null_Compare_SortsLast()
    {
        // Data.Null() carries the singleton; Compare's null policy coalesces it to
        // a C# null and sorts it last (the policy lives on Compare, not the wrapper).
        var present = new Data("", ((global::app.type.item.number.@this)(5L)), context: app.actor.list.User.Context);
        var nul = app.actor.list.User.Context.Null();
        await Assert.That(CompareTestOps.OrdD(present, nul)).IsLessThan(0);
        await Assert.That(CompareTestOps.OrdD(nul, present)).IsGreaterThan(0);
        await Assert.That(CompareTestOps.OrdD(nul, app.actor.list.User.Context.Null())).IsEqualTo(0);
    }

    [Test]
    public async Task Null_IsValueNotAbsence_DataIsInitializedDistinction()
    {
        // A Data carrying null.@this is a PRESENT null — IsInitialized = true and
        // its Value is the singleton. A missing var (NotFound/Uninitialized) is the
        // ABSENCE of a value — IsInitialized = false. Two different axes.
        var presentNull = app.actor.list.User.Context.Null();
        await Assert.That(presentNull.IsInitialized).IsTrue();
        await Assert.That(presentNull.Peek() is NullV).IsTrue();

        var missing = app.actor.list.User.Context.NotFound("x");
        await Assert.That(missing.IsInitialized).IsFalse();
    }
}
