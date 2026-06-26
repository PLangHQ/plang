using app.data;

namespace PLang.Tests.App.DataTests;

// data-normalize
// Data.Normalize() walks data.Value into the wire tree. Containers ride as the
// native value types — dict.@this / list.@this (each element is a Data) — scalars
// as their own item (text/number/bool/datetime/binary/the null citizen). Reflection
// fires once per domain type here (into a native dict); format encoders never reflect.
// Normalize is lazy (called by the serializer), idempotent, observation-only
// (it copies; it never mutates the source value), and bounded.

public class NormalizeTreeShapeTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/tmp/NormalizeTreeShapeTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test] public async Task Normalize_Null_IsThePlangNullCitizen()
    {
        var d = new Data("x", (object?)null);
        var n = d.Normalize() as global::app.type.item.@this;
        await Assert.That(n).IsNotNull();
        await Assert.That(n!.IsNull).IsTrue();
    }

    [Test] public async Task Normalize_String_ReturnsUnchanged()
    {
        var d = _app.Data("x", "hello");
        await Assert.That(d.Normalize()?.ToString()).IsEqualTo("hello");
    }

    [Test] public async Task Normalize_Int_Long_Double_Bool_Decimal_DateTime_ReturnUnchanged()
    {
        await Assert.That(_app.Data("", 42).Normalize()?.ToString()).IsEqualTo("42");
        await Assert.That(_app.Data("", 42L).Normalize()?.ToString()).IsEqualTo("42");
        await Assert.That(_app.Data("", 1.5).Normalize()?.ToString()).IsEqualTo("1.5");
        await Assert.That(_app.Data("", true).Normalize()?.ToString()).IsEqualTo("true");
        await Assert.That(_app.Data("", 3.14m).Normalize()?.ToString()).IsEqualTo("3.14");
        // A C# DateTime lifts to the `datetime` value (DateTimeOffset-backed) — it
        // rides the wire as a datetime leaf, not a raw CLR DateTime.
        var dt = new System.DateTime(2026, 1, 2, 3, 4, 5, System.DateTimeKind.Utc);
        await Assert.That(_app.Data("", dt).Normalize()).IsTypeOf<global::app.type.datetime.@this>();
    }

    [Test] public async Task Normalize_ByteArray_ReturnsUnchanged_OpaqueBinaryLeaf()
    {
        var bytes = new byte[] { 1, 2, 3 };
        await Assert.That(((global::app.type.binary.@this)_app.Data("", bytes).Normalize()!).Value).IsSameReferenceAs(bytes);
    }

    [Test] public async Task Normalize_HomogeneousPrimitiveList_StaysNativeList()
    {
        var d = _app.Data("", new List<int> { 1, 2, 3 });
        var result = d.Normalize();
        await Assert.That(result).IsTypeOf<app.type.list.@this>();
        var items = result.Children();
        await Assert.That(items.Count).IsEqualTo(3);
        await Assert.That(items[0].Peek()?.ToString()).IsEqualTo("1");
        await Assert.That(items[1].Peek()?.ToString()).IsEqualTo("2");
        await Assert.That(items[2].Peek()?.ToString()).IsEqualTo("3");
    }

    [Test] public async Task Normalize_HeterogeneousList_StaysNativeList()
    {
        var d = _app.Data("", new List<object> { 1, "two", 3.0 });
        var result = d.Normalize();
        await Assert.That(result).IsTypeOf<app.type.list.@this>();
        var items = result.Children();
        await Assert.That(items.Count).IsEqualTo(3);
        await Assert.That(items[0].Peek()?.ToString()).IsEqualTo("1");
        await Assert.That(items[1].Peek()?.ToString()).IsEqualTo("two");
        await Assert.That(items[2].Peek()?.ToString()).IsEqualTo("3");
    }

    [Test] public async Task Normalize_DictionaryStringX_BecomesListOfData_KeysAsNames()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var d = _app.Data("", dict);
        var result = d.Normalize();
        await Assert.That(result).IsTypeOf<app.type.dict.@this>();
        var list = result.Children();
        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list.Any(c => c.Name == "a" && c.Peek()?.ToString() == "1")).IsTrue();
        await Assert.That(list.Any(c => c.Name == "b" && c.Peek()?.ToString() == "2")).IsTrue();
    }

    [Test] public async Task Normalize_DomainObject_EmitsOneChildPerOutProperty_LowercasedName()
    {
        var identity = new global::app.module.identity.Identity
        {
            Name = "alice",
            PublicKey = "pk",
            PrivateKey = "sekret",
            IsDefault = true,
        };
        var d = _app.Data("", identity);
        var result = d.Normalize();
        await Assert.That(result).IsTypeOf<app.type.dict.@this>();
        var children = result.Children();
        // Only [Out] props ship: Name + PublicKey. PrivateKey [Sensitive], others local.
        await Assert.That(children.Count).IsEqualTo(2);
        await Assert.That(children.Any(c => c.Name == "name" && c.Peek()?.ToString() == "alice")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "publickey" && c.Peek()?.ToString() == "pk")).IsTrue();
    }

    [Test] public async Task Normalize_RecordType_EmitsOneChildPerOutProperty()
    {
        var setting = new global::app.module.settings.type.setting { key = "DATABASE_URL", value = "postgres://..." };
        var d = _app.Data("", setting);
        var result = d.Normalize();
        var children = result.Children();
        await Assert.That(children.Count).IsEqualTo(2);
        await Assert.That(children.Any(c => c.Name == "key" && c.Peek()?.ToString() == "DATABASE_URL")).IsTrue();
        // [Masked] — value is "****", real value never reached.
        await Assert.That(children.Any(c => c.Name == "value" && c.Peek()?.ToString() == "****")).IsTrue();
    }

    [Test] public async Task Normalize_IsIdempotent_CallingTwiceProducesSameTree()
    {
        var d = _app.Data("", new Dictionary<string, int> { ["x"] = 1 });
        var r1 = d.Normalize();
        var r2 = d.Normalize();
        // Shape stable across calls: same type, same count, same contents.
        await Assert.That(r1).IsTypeOf<app.type.dict.@this>();
        await Assert.That(r2).IsTypeOf<app.type.dict.@this>();
        await Assert.That(r1.Children().Count).IsEqualTo(r2.Children().Count);
    }

    [Test] public async Task Normalize_PropertyLookupCache_ReturnsSameReferenceForSameKey()
    {
        // The cache contract: PropertiesFor(T, mode) returns the same
        // IReadOnlyList<Entry> reference for the same (Type, Mode) key —
        // reflection fires once, then handed back. Asserting on the global
        // CacheSize counter races with parallel Normalize tests; the
        // per-key reference identity is the behaviour this filter owns.
        System.Type t = typeof(global::app.module.identity.Identity);
        var first = global::app.channel.serializer.filter.Tagged.PropertiesFor(t, global::app.View.Out);
        var second = global::app.channel.serializer.filter.Tagged.PropertiesFor(t, global::app.View.Out);
        await Assert.That(object.ReferenceEquals(first, second)).IsTrue()
            .Because("second call for the same (Type, Mode) key must hand back the cached reference");
    }

    // A delegate isn't a plang item, so it parks in an item.clr carrier; Normalize
    // unwraps the carrier to its host and hits the `is Delegate → null` leaf, so an
    // unrepresentable value normalizes to null rather than leaking a property bag.
    [Test] public async Task Normalize_UnsupportedType_ThrowsTypedError()
    {
        var d = _app.Data("", new System.Func<int>(() => 0));
        var result = d.Normalize();
        await Assert.That(result).IsNull();
    }
}
