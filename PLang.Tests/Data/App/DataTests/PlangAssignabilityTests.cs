namespace PLang.Tests.App.DataTests;

// The string-not-iterable rule, post-consumer-tail: the predicate helpers
// (IsPlangIterable / IsPlangAssignable / AsEnumerable) are gone — iteration is
// the collection types' own member, and text refuses char-iteration by NOT
// implementing IEnumerable. These pin the surviving outcomes.

public class PlangAssignabilityTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    [Test]
    public async Task Text_DoesNotImplementIEnumerable()
    {
        await Assert.That(typeof(System.Collections.IEnumerable)
            .IsAssignableFrom(typeof(global::app.type.text.@this))).IsFalse();
    }

    [Test]
    public async Task IterationHelpers_AreGone_FromData()
    {
        var t = typeof(Data);
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
        await Assert.That(t.GetMethod("AsEnumerable", flags)).IsNull();
        await Assert.That(t.GetMethod("IsPlangAssignable", flags)).IsNull();
    }

    // foreach %s% never char-iterates: EnumerateItems on a text-valued Data
    // yields exactly one item — the value itself.
    [Test]
    public async Task EnumerateItems_TextValue_YieldsOneWholeItem()
    {
        var source = new Data("text", "hello") { Context = _app.User.Context };
        var items = new List<global::app.data.@this>();
        foreach (var (_, item) in source.EnumerateItems()) items.Add(item);
        await Assert.That(items.Count).IsEqualTo(1);
        await Assert.That(items[0].Peek()?.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task EnumerateItems_NumberValue_YieldsOneWholeItem()
    {
        var source = new Data("n", 42) { Context = _app.User.Context };
        var items = new List<global::app.data.@this>();
        foreach (var (_, item) in source.EnumerateItems()) items.Add(item);
        await Assert.That(items.Count).IsEqualTo(1);
    }
}
