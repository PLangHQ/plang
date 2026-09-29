using Dict = global::app.type.item.dict.@this;

namespace PLang.Tests.App.CollectionsAreData;

// Stage 1 — `dict` is the native object type.
// New value type at app/type/dict/, mirrors app/type/path/. Holds Dictionary<string,data>,
// owns Get/Keys/Has, implements IBooleanResolvable, and is registered in the primitive map.
// These tests pin the value-type surface in isolation, before navigator / writer / parser
// repointing in Stage1_DictNavigationAndWriterTests.
public class Stage1_DictValueTypeTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/tmp/Stage1DictVT-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task Get_ExistingKey_ReturnsDataValue()
    {
        // dict.Get("name") on a dict holding {name:Data("a")} returns that element Data.
        var d = new Dict();
        d.Set(app.Data("name", "a"));
        var entry = d.Get("name", app.actor.list.User.Context);
        await Assert.That(entry).IsNotNull();
        await Assert.That((await entry!.Value())?.ToString()).IsEqualTo("a");
    }

    [Test]
    public async Task Get_MissingKey_ReturnsNull()
    {
        // dict.Get on an unknown key returns null (not throws) — caller decides what missing means.
        var d = new Dict();
        d.Set(app.Data("name", "a"));
        await Assert.That(d.Get("nope", app.actor.list.User.Context)).IsNull();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Keys_PreservesInsertionOrder()
    {
        // Keys enumerates in insertion order — round-trip stability for round-tripped json objects.
        var d = new Dict();
        d.Set(app.Data("name", "a"));
        d.Set(app.Data("age", 30L));
        d.Set(app.Data("city", "Reyk"));
        // Keys is the typed list<text> surface; assert over the text values.
        await Assert.That(d.Keys.Items(app.actor.list.User.Context).Select(k => k.Peek()?.ToString()).ToList())
            .IsEquivalentTo(new[] { "name", "age", "city" });
    }

    [Test]
    public async Task Has_KnownKey_ReturnsTrue()
    {
        // Has(name) is true for a present key.
        var d = new Dict();
        d.Set(app.Data("name", "a"));
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
        await Assert.That(await d.AsBooleanAsync(app.actor.list.User.Context)).IsFalse();
    }

    [Test]
    public async Task AsBooleanAsync_NonEmptyDict_IsTrue()
    {
        // IBooleanResolvable: a dict with any entry is truthy.
        var d = new Dict();
        d.Set(app.Data("name", "a"));
        await Assert.That(await d.AsBooleanAsync(app.actor.list.User.Context)).IsTrue();
    }

    [Test]
    public async Task PrimitiveMap_DictRegistered_RawDictionaryEntryRetired()
    {
        // "dict" and its aliases name the dict value type, not a raw Dictionary.
        var types = app.actor.list.User.Context.App.type.list;
        await Assert.That(types.Clr("dict")).IsEqualTo(typeof(Dict));
        await Assert.That(types.Clr("dictionary")).IsEqualTo(typeof(Dict));
        await Assert.That(types.Clr("map")).IsEqualTo(typeof(Dict));
        await Assert.That(types.Clr("dict")).IsNotEqualTo(typeof(Dictionary<string, object>));
    }
}
