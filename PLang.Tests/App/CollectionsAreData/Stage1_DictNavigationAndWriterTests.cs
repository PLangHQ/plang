namespace PLang.Tests.App.CollectionsAreData;

// Stage 1 — the wiring around dict: navigator collapses into it, json writer
// disambiguates by type (no property-bag arm), and the lazy parse seam narrows
// json objects to dict (not raw Dictionary<string,object?>).
public class Stage1_DictNavigationAndWriterTests
{
    [Test]
    public async Task Materialize_JsonObjectRoot_NarrowsToDict()
    {
        // A Data carrying raw json bytes for `{...}` (kind=json), on first navigation,
        // materializes Value as a dict — not a raw Dictionary<string,object?> (B+J).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task DictionaryNavigator_CollapsesToValueType()
    {
        // variable/navigator/Dictionary collapses: when data.Value is dict, navigation
        // is `d.Get(key)`. The three-arm shape dispatch (IDictionary / generic IDictionary<,>
        // / JsonObject) and the reflection fallback are gone for the dict case (C).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task JsonWriter_PropertyBagArm_Deleted()
    {
        // writer.cs no longer has `case List<app.data.@this> propertyBag:` (E).
        // A dict serializes via its own renderer; nothing else routes to `{}`-by-named-Data.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task NormalizeObject_DomainRecord_ReturnsDict()
    {
        // data/this.Normalize.cs's NormalizeObject returns a dict for a C# domain record
        // (e.g. permission) — not List<@this> (F). One object shape across the wire.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
