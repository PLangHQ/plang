using System.Collections;

namespace PLang.Tests.App.DataTests;

// Phase 2c+2d contract — plang-specific assignability and the string-not-iterable
// rule. Three places in the codebase need to agree on "is this value iterable
// as a plang collection?": As<T>'s variance fast path, Data.AsEnumerable, and
// Data.EnumerateItems. They route through one IsPlangIterable predicate so the
// rule has a single source of truth.

public class PlangAssignabilityTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    [Test]
    public async Task IsPlangIterable_String_ReturnsFalse()
    {
        await Assert.That(Data.IsPlangIterable("hello")).IsFalse();
    }

    [Test]
    public async Task IsPlangIterable_List_ReturnsTrue()
    {
        await Assert.That(Data.IsPlangIterable(new List<int> { 1, 2, 3 })).IsTrue();
    }

    [Test]
    public async Task IsPlangIterable_Null_ReturnsFalse()
    {
        await Assert.That(Data.IsPlangIterable(null)).IsFalse();
    }

    [Test]
    public async Task IsPlangAssignable_StringToIEnumerable_ReturnsFalse()
    {
        await Assert.That(Data.IsPlangAssignable(typeof(IEnumerable), typeof(string))).IsFalse();
    }

    [Test]
    public async Task IsPlangAssignable_ListToIEnumerable_ReturnsTrue()
    {
        await Assert.That(Data.IsPlangAssignable(typeof(IEnumerable), typeof(List<int>))).IsTrue();
    }

    // Behavioral consequence of the carve-out: Data<global::app.type.text.@this>("hello").As<IEnumerable>()
    // wraps the string as a single-element IEnumerable. Iterating wrapped.Value yields
    // exactly one item: the whole string "hello". NOT 'h', 'e', 'l', 'l', 'o'.
    [Test]
    public async Task AsT_StringToIEnumerable_WrapsAsSingleElementList()
    {
        var source = new global::app.data.@this<global::app.type.text.@this>("text", "hello") { Context = _app.User.Context };
        var items = new List<object?>();
        foreach (var item in source.AsEnumerable()) items.Add(item);
        await Assert.That(items.Count).IsEqualTo(1);
        await Assert.That(items[0]!.ToString()).IsEqualTo("hello");
    }

    // Same shape for any non-iterable scalar — Data<global::app.type.number.@this>(42).As<IEnumerable>()
    // wraps as a single-element sequence yielding 42.
    [Test]
    public async Task AsT_IntToIEnumerable_WrapsAsSingleElementList()
    {
        var source = new global::app.data.@this<global::app.type.number.@this>("n", 42) { Context = _app.User.Context };
        var items = new List<object?>();
        foreach (var item in source.AsEnumerable()) items.Add(item);
        await Assert.That(items.Count).IsEqualTo(1);
        await Assert.That(items[0]).IsEqualTo(42);
    }

    // Single-source-of-truth check: AsEnumerable() on a Data containing a
    // string yields ONE item (the string itself).
    [Test]
    public async Task AsEnumerable_DelegatesToSharedPredicate_StringNotIterable()
    {
        var source = new Data("text", "hello") { Context = _app.User.Context };
        var seq = source.AsEnumerable();
        var items = new List<object?>();
        foreach (var item in seq) items.Add(item);
        await Assert.That(items.Count).IsEqualTo(1);
        await Assert.That(items[0]!.ToString()).IsEqualTo("hello");
    }
}
