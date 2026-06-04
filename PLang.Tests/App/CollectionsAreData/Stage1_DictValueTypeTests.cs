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
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Get_MissingKey_ReturnsNull()
    {
        // dict.Get on an unknown key returns null (not throws) — caller decides what missing means.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Keys_PreservesInsertionOrder()
    {
        // Keys enumerates in insertion order — round-trip stability for round-tripped json objects.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Has_KnownKey_ReturnsTrue()
    {
        // Has(name) is true for a present key.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Has_MissingKey_ReturnsFalse()
    {
        // Has(name) is false for an absent key — distinct from Get returning null.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task AsBooleanAsync_EmptyDict_IsFalse()
    {
        // IBooleanResolvable: empty dict is falsy — matches falsiness of empty list/string/null.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task AsBooleanAsync_NonEmptyDict_IsTrue()
    {
        // IBooleanResolvable: a dict with any entry is truthy.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task JsonSerialize_EmptyDict_EmitsEmptyObject()
    {
        // dict's own renderer emits `{}` for an empty dict — not `[]` and not the property-bag arm.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task JsonSerialize_TwoEntries_EmitsKeyedObject()
    {
        // dict emits {"name":"a","age":30} — keyed by entry name, values via per-element renderer.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task JsonSerialize_NestedDict_EmitsNestedObject()
    {
        // dict holding a dict emits {"address":{"city":"Reyk"}} — nesting works through one renderer.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task PrimitiveMap_DictRegistered_RawDictionaryEntryRetired()
    {
        // type/primitive/this.cs maps "dict" to the new value type; the raw
        // Dictionary<string,object> entry that used to back "dict" is gone (J).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
