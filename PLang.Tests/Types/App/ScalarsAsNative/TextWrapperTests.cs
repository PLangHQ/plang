using Text = global::app.type.text.@this;
using Item = global::app.type.item.@this;

using PLang.Tests.App.Fixtures;

namespace PLang.Tests.App.ScalarsAsNative;

// text.@this is the canonical wrapper for textual content; backs a CLR string.
// After build-out it carries ops, ordinal compare, value-equality + GetHashCode,
// truthiness, and atomicity (NOT IEnumerable as chars). Order/equality are
// ordinal case-insensitive — matching the historical ScalarComparer policy.
public class TextWrapperTests
{
    [Test]
    public async Task Text_Length_ReturnsCodepointCount()
    {
        // Length via a method on the wrapper, not via raw string fallback.
        await Assert.That(new Text("hello").Length).IsEqualTo(5);
        await Assert.That(new Text("").Length).IsEqualTo(0);
    }

    [Test]
    public async Task Text_CaseAndTrim_ReturnTextNotRawString()
    {
        // upper/lower/trim return text.@this, not raw string — flow stays native.
        Text upper = new Text("abc").Upper();
        Text lower = new Text("ABC").Lower();
        Text trimmed = new Text("  x  ").Trim();
        await Assert.That(upper.ToString()).IsEqualTo("ABC");
        await Assert.That(lower.ToString()).IsEqualTo("abc");
        await Assert.That(trimmed.ToString()).IsEqualTo("x");
    }

    [Test]
    public async Task Text_ContainsStartsEndsWith_BehavioralOps()
    {
        // contains/startsWith/endsWith on the wrapper; case-insensitive policy.
        var s = new Text("Hello World");
        await Assert.That(s.Contains("world")).IsTrue();
        await Assert.That(s.StartsWith("hello")).IsTrue();
        await Assert.That(s.EndsWith("WORLD")).IsTrue();
        await Assert.That(s.Contains("xyz")).IsFalse();
    }

    [Test]
    public async Task Text_Substring_BehavioralOp()
    {
        // substring(start, len) returns text.
        await Assert.That(new Text("hello").Substring(1, 3).ToString()).IsEqualTo("ell");
    }

    [Test]
    public async Task Text_Order_OrdinalCompare()
    {
        // text orders ordinally through its Compare hook; "a" < "b".
        await Assert.That(CompareTestOps.Ord(new Text("a"), new Text("b"))).IsLessThan(0);
        await Assert.That(CompareTestOps.Ord(new Text("b"), new Text("a"))).IsGreaterThan(0);
        await Assert.That(CompareTestOps.Ord(new Text("a"), new Text("A"))).IsEqualTo(0); // case-insensitive
    }

    [Test]
    public async Task Text_Equality_TwoSameValueAreEqualAndHashEqual()
    {
        // text("a") and text("a") are Equal AND share GetHashCode — usable as a
        // dict key and inside a HashSet without surprise.
        var a1 = new Text("a");
        var a2 = new Text("a");
        await Assert.That(a1.Equals(a2)).IsTrue();
        await Assert.That(a1.GetHashCode()).IsEqualTo(a2.GetHashCode());
        var set = new HashSet<Text> { a1 };
        await Assert.That(set.Contains(a2)).IsTrue();
    }

    [Test]
    public async Task Text_Equality_RawStringAndTextResolveConsistentlyInHashSet()
    {
        // Mid-migration aliasing guard: the implicit text↔string operator compiles
        // but does NOT make a raw string "a" hash-equal to a text.@this("a").
        // A HashSet<object> populated across a not-yet-swept window must not
        // silently treat the two as the same member.
        var set = new HashSet<object> { new Text("a") };
        await Assert.That(set.Contains("a")).IsFalse();          // raw string ≠ text member
        await Assert.That(set.Contains(new Text("a"))).IsTrue(); // text ≡ text member
    }

    [Test]
    public async Task Text_Truthiness_EmptyFalsyNonEmptyTruthy()
    {
        // text("") falsy via item truthiness; text("x") truthy. Sync path.
        Item empty = new Text("");
        Item nonEmpty = new Text("x");
        await Assert.That(empty.IsTruthy()).IsFalse();
        await Assert.That(nonEmpty.IsTruthy()).IsTrue();
        await Assert.That(await empty.AsBooleanAsync()).IsFalse();
    }

    [Test]
    public async Task Text_IsNotIEnumerable_ForeachDoesNotCharIterate()
    {
        // text.@this must not implement IEnumerable (chars) — it is an atomic scalar.
        await Assert.That(typeof(System.Collections.IEnumerable).IsAssignableFrom(typeof(Text))).IsFalse();
    }
}
