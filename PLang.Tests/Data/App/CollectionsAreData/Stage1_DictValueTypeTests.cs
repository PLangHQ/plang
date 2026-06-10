using PLang.Tests.App.Serialization;
using Dict = global::app.type.dict.@this;

namespace PLang.Tests.App.CollectionsAreData;

// Stage 1 — `dict` is the native object type.
// New value type at app/type/dict/, mirrors app/type/path/. Holds Dictionary<string,data>,
// owns Get/Keys/Has, implements IBooleanResolvable, and is registered in the primitive map.
// These tests pin the value-type surface in isolation, before navigator / writer / parser
// repointing in Stage1_DictNavigationAndWriterTests.
public class Stage1_DictValueTypeTests
{
    [Test]
    public async Task Get_ExistingKey_ReturnsDataValue()
    {
        // dict.Get("name") on a dict holding {name:Data("a")} returns that element Data.
        var d = new Dict();
        d.Set(new Data("name", "a"));
        var entry = d.Get("name");
        await Assert.That(entry).IsNotNull();
        await Assert.That((await entry!.Value())?.ToString()).IsEqualTo("a");
    }

    [Test]
    public async Task Get_MissingKey_ReturnsNull()
    {
        // dict.Get on an unknown key returns null (not throws) — caller decides what missing means.
        var d = new Dict();
        d.Set(new Data("name", "a"));
        await Assert.That(d.Get("nope")).IsNull();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Keys_PreservesInsertionOrder()
    {
        // Keys enumerates in insertion order — round-trip stability for round-tripped json objects.
        var d = new Dict();
        d.Set(new Data("name", "a"));
        d.Set(new Data("age", 30L));
        d.Set(new Data("city", "Reyk"));
        // Keys is the typed list<text> surface; assert over the text values.
        await Assert.That(d.Keys.Items.Select(k => k.Peek()?.ToString()).ToList())
            .IsEquivalentTo(new[] { "name", "age", "city" });
    }

    [Test]
    public async Task Has_KnownKey_ReturnsTrue()
    {
        // Has(name) is true for a present key.
        var d = new Dict();
        d.Set(new Data("name", "a"));
        await Assert.That(d.Has("name")).IsTrue();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Has_MissingKey_ReturnsFalse()
    {
        // Has(name) is false for an absent key — distinct from Get returning null.
        var d = new Dict();
        await Assert.That(d.Has("name")).IsFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task AsBooleanAsync_EmptyDict_IsFalse()
    {
        // IBooleanResolvable: empty dict is falsy — matches falsiness of empty list/string/null.
        var d = new Dict();
        await Assert.That(await d.AsBooleanAsync()).IsFalse();
    }

    [Test]
    public async Task AsBooleanAsync_NonEmptyDict_IsTrue()
    {
        // IBooleanResolvable: a dict with any entry is truthy.
        var d = new Dict();
        d.Set(new Data("name", "a"));
        await Assert.That(await d.AsBooleanAsync()).IsTrue();
    }

    [Test]
    public async Task JsonSerialize_EmptyDict_EmitsEmptyObject()
    {
        // dict's own renderer emits `{}` for an empty dict — not `[]` and not the property-bag arm.
        var json = NormalizePipelineHelper.SerializeValueSlot(new Dict());
        await Assert.That(json).IsEqualTo("{}");
    }

    [Test]
    public async Task JsonSerialize_TwoEntries_EmitsKeyedObject()
    {
        // dict emits {"name":"a","age":30} — keyed by entry name, values via per-element renderer.
        var d = new Dict();
        d.Set(new Data("name", "a"));
        d.Set(new Data("age", 30L));
        var json = NormalizePipelineHelper.SerializeValueSlot(d);
        await Assert.That(json).IsEqualTo("{\"name\":\"a\",\"age\":30}");
    }

    [Test]
    public async Task JsonSerialize_NestedDict_EmitsNestedObject()
    {
        // dict holding a dict emits {"address":{"city":"Reyk"}} — nesting works through one renderer.
        var inner = new Dict();
        inner.Set(new Data("city", "Reyk"));
        var outer = new Dict();
        outer.Set(new Data("address", inner));
        var json = NormalizePipelineHelper.SerializeValueSlot(outer);
        await Assert.That(json).IsEqualTo("{\"address\":{\"city\":\"Reyk\"}}");
    }

    [Test]
    public async Task PrimitiveMap_DictRegistered_RawDictionaryEntryRetired()
    {
        // type/primitive/this.cs maps "dict" to the new value type; the raw
        // Dictionary<string,object> entry that used to back "dict" is gone (J).
        var aliases = global::app.type.primitive.@this.Aliases;
        await Assert.That(aliases["dict"]).IsEqualTo(typeof(Dict));
        await Assert.That(aliases["dictionary"]).IsEqualTo(typeof(Dict));
        await Assert.That(aliases["map"]).IsEqualTo(typeof(Dict));
        await Assert.That(aliases["dict"]).IsNotEqualTo(typeof(Dictionary<string, object>));
    }
}
