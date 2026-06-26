namespace PLang.Tests.App.DataTests;

// Phase 1 contract — events on Data are mutable Lists, not C# multicast delegates.
// Why it matters: the architect's identity-preservation story relies on the four
// state slots (Properties + OnCreate + OnChange + OnDelete) being aliasable by
// reference between source Data and a cross-type wrap. C# events (immutable
// multicast delegates) can't be ref-shared.
//
// These tests pin the list shape from the consumer's perspective. AsTIdentityTests
// builds on this foundation. SubscriberSurvivalTests is gone (per Ingi: Variables.Set
// is dumb storage; clone semantics live in variable.set, not in replacement merging).

public class EventListTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/tmp/EventListTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    // Consumer-facing API: OnCreate is a List<Action<Data>>. .Add and .Remove
    // work; the property type itself is a List, not a `event Action<Data>`.
    [Test]
    public async Task OnCreate_IsListType_NotEvent()
    {
        var d = new Data("x");
        await Assert.That(d.OnCreate).IsNotNull();
        await Assert.That(d.OnCreate).IsTypeOf<List<Action<Data>>>();
        d.OnCreate.Add(_ => { });
        await Assert.That(d.OnCreate.Count).IsEqualTo(1);
    }

    // OnChange is a List<Action<Data, Data>> — two Data args (oldData, newData).
    [Test]
    public async Task OnChange_IsListType_NotEvent()
    {
        var d = new Data("x");
        await Assert.That(d.OnChange).IsNotNull();
        await Assert.That(d.OnChange).IsTypeOf<List<Action<Data, Data>>>();
        d.OnChange.Add((_, _) => { });
        await Assert.That(d.OnChange.Count).IsEqualTo(1);
    }

    // OnDelete is a List<Action<Data>> — single Data arg (the deleted Data).
    [Test]
    public async Task OnDelete_IsListType_NotEvent()
    {
        var d = new Data("x");
        await Assert.That(d.OnDelete).IsNotNull();
        await Assert.That(d.OnDelete).IsTypeOf<List<Action<Data>>>();
    }

    // Multicast is preserved across the migration: two subscribers added in order,
    // FireOnChange invokes both with (this, newData). Order is insertion order so
    // the test asserts a deterministic call sequence.
    [Test]
    public async Task FireOnChange_InvokesAllSubscribersInOrder()
    {
        var d = _app.Data("x", 1);
        var newD = _app.Data("x", 2);
        var calls = new List<int>();
        d.OnChange.Add((_, _) => calls.Add(1));
        d.OnChange.Add((_, _) => calls.Add(2));
        d.FireOnChange(newD);
        await Assert.That(calls).IsEquivalentTo(new[] { 1, 2 });
    }

    // A subscriber added AFTER construction (not inline at init) sees a subsequent
    // FireOnCreate. With C# events this was implicit; with Lists we pin it so a
    // future "freeze on construction" optimization can't quietly break it.
    [Test]
    public async Task FireOnCreate_SubscriberAddedAfterInit_StillFires()
    {
        var d = new Data("x");
        var seen = false;
        d.OnCreate.Add(_ => seen = true);
        d.FireOnCreate();
        await Assert.That(seen).IsTrue();
    }

    // Precondition for AsTIdentityTests: two unrelated Data instances do NOT share
    // their event lists by default. Aliasing only happens via As<T> wrapping —
    // never by construction. Without this baseline, the aliasing tests are vacuous.
    [Test]
    public async Task EventLists_TwoDataInstances_HoldDistinctListsByDefault()
    {
        var a = new Data("a");
        var b = new Data("b");
        await Assert.That(ReferenceEquals(a.OnCreate, b.OnCreate)).IsFalse();
        await Assert.That(ReferenceEquals(a.OnChange, b.OnChange)).IsFalse();
        await Assert.That(ReferenceEquals(a.OnDelete, b.OnDelete)).IsFalse();
    }
}
