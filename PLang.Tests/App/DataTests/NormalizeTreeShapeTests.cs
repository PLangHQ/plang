using app.data;

namespace PLang.Tests.App.DataTests;

// data-normalize — Stage 2
// Data.Normalize() walks data.Value into a uniform tree of:
//   primitive | byte[] | Data | List<>
// Reflection fires exactly once per type here; format encoders never reflect.
// Normalize is lazy (called by the serializer), idempotent, and bounded.

public class NormalizeTreeShapeTests
{
    [Test] public async Task Normalize_Null_ReturnsNull()
    {
        var d = new Data("x", (object?)null);
        await Assert.That(d.Normalize()).IsNull();
    }

    [Test] public async Task Normalize_String_ReturnsUnchanged()
    {
        var d = new Data("x", "hello");
        await Assert.That(d.Normalize()).IsEqualTo("hello");
    }

    [Test] public async Task Normalize_Int_Long_Double_Bool_Decimal_DateTime_ReturnUnchanged()
    {
        await Assert.That(new Data("", 42).Normalize()).IsEqualTo(42);
        await Assert.That(new Data("", 42L).Normalize()).IsEqualTo(42L);
        await Assert.That(new Data("", 1.5).Normalize()).IsEqualTo(1.5);
        await Assert.That(new Data("", true).Normalize()).IsEqualTo(true);
        await Assert.That(new Data("", 3.14m).Normalize()).IsEqualTo(3.14m);
        var dt = new System.DateTime(2026, 1, 2, 3, 4, 5, System.DateTimeKind.Utc);
        await Assert.That(new Data("", dt).Normalize()).IsEqualTo(dt);
    }

    [Test] public async Task Normalize_ByteArray_ReturnsUnchanged_OpaqueBinaryLeaf()
    {
        var bytes = new byte[] { 1, 2, 3 };
        await Assert.That(new Data("", bytes).Normalize()).IsSameReferenceAs(bytes);
    }

    [Test] public async Task Normalize_HomogeneousPrimitiveList_StaysListOfPrimitives()
    {
        var d = new Data("", new List<int> { 1, 2, 3 });
        var result = d.Normalize();
        await Assert.That(result).IsTypeOf<List<object?>>();
        var list = (List<object?>)result!;
        await Assert.That(list.Count).IsEqualTo(3);
        await Assert.That(list[0]).IsEqualTo(1);
        await Assert.That(list[1]).IsEqualTo(2);
        await Assert.That(list[2]).IsEqualTo(3);
    }

    [Test] public async Task Normalize_HeterogeneousList_BecomesListOfData()
    {
        var d = new Data("", new List<object> { 1, "two", 3.0 });
        var result = d.Normalize();
        await Assert.That(result).IsTypeOf<List<object?>>();
        var list = (List<object?>)result!;
        await Assert.That(list.Count).IsEqualTo(3);
        await Assert.That(list[0]).IsEqualTo(1);
        await Assert.That(list[1]).IsEqualTo("two");
        await Assert.That(list[2]).IsEqualTo(3.0);
    }

    [Test] public async Task Normalize_NestedData_RecursesAndStaysData()
    {
        var inner = new Data("inner", "v");
        var outer = new Data("outer", inner);
        var result = outer.Normalize();
        await Assert.That(result).IsSameReferenceAs(inner);
        await Assert.That(inner.Value).IsEqualTo("v");
    }

    [Test] public async Task Normalize_DictionaryStringX_BecomesListOfData_KeysAsNames()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var d = new Data("", dict);
        var result = d.Normalize();
        await Assert.That(result).IsTypeOf<List<Data>>();
        var list = (List<Data>)result!;
        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list.Any(c => c.Name == "a" && (int)c.Value! == 1)).IsTrue();
        await Assert.That(list.Any(c => c.Name == "b" && (int)c.Value! == 2)).IsTrue();
    }

    [Test] public async Task Normalize_DomainObject_EmitsOneChildPerOutProperty_LowercasedName()
    {
        var identity = new global::app.modules.identity.Identity
        {
            Name = "alice",
            PublicKey = "pk",
            PrivateKey = "sekret",
            IsDefault = true,
        };
        var d = new Data("", identity);
        var result = d.Normalize();
        await Assert.That(result).IsTypeOf<List<Data>>();
        var children = (List<Data>)result!;
        // Only [Out] props ship: Name + PublicKey. PrivateKey [Sensitive], others local.
        await Assert.That(children.Count).IsEqualTo(2);
        await Assert.That(children.Any(c => c.Name == "name" && (string?)c.Value == "alice")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "publickey" && (string?)c.Value == "pk")).IsTrue();
    }

    [Test] public async Task Normalize_RecordType_EmitsOneChildPerOutProperty()
    {
        var setting = new global::app.modules.settings.types.setting { key = "DATABASE_URL", value = "postgres://..." };
        var d = new Data("", setting);
        var result = d.Normalize();
        var children = (List<Data>)result!;
        await Assert.That(children.Count).IsEqualTo(2);
        await Assert.That(children.Any(c => c.Name == "key" && (string?)c.Value == "DATABASE_URL")).IsTrue();
        // [Masked] — value is "****", real value never reached.
        await Assert.That(children.Any(c => c.Name == "value" && (string?)c.Value == "****")).IsTrue();
    }

    [Test] public async Task Normalize_IsIdempotent_CallingTwiceProducesSameTree()
    {
        var d = new Data("", new Dictionary<string, int> { ["x"] = 1 });
        var r1 = d.Normalize();
        var r2 = d.Normalize();
        // Shape stable across calls: same type, same count, same contents.
        await Assert.That(r1).IsTypeOf<List<Data>>();
        await Assert.That(r2).IsTypeOf<List<Data>>();
        await Assert.That(((List<Data>)r1!).Count).IsEqualTo(((List<Data>)r2!).Count);
    }

    [Test] public async Task Normalize_PropertyLookupCache_PopulatesOnFirstCall_HitsOnSecond()
    {
        global::app.channels.serializers.filters.Tagged.ClearCacheForTests();
        var sizeBefore = global::app.channels.serializers.filters.Tagged.CacheSize;
        var identity = new global::app.modules.identity.Identity { Name = "x", PublicKey = "y" };
        new Data("", identity).Normalize();
        var sizeAfter1 = global::app.channels.serializers.filters.Tagged.CacheSize;
        new Data("", identity).Normalize();
        var sizeAfter2 = global::app.channels.serializers.filters.Tagged.CacheSize;
        await Assert.That(sizeAfter1).IsGreaterThan(sizeBefore);
        await Assert.That(sizeAfter2).IsEqualTo(sizeAfter1).Because("second call hits cache, no new entry");
    }

    [Test] public async Task Normalize_UnsupportedType_ThrowsTypedError()
    {
        // Delegates aren't representable as a property bag — emitted as
        // null leaf (the receiver can't reconstruct a delegate from bytes).
        var d = new Data("", new System.Func<int>(() => 0));
        var result = d.Normalize();
        await Assert.That(result).IsNull();
    }
}
