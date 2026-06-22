using System.Text.Json.Nodes;

namespace PLang.Tests.App.DataTests.Navigators;

// A JsonObject is NEVER navigated raw. Every JSON value is narrowed to a native
// dict/list at the boundary: Data.SetValue does Lift(json.Parse(value)), and
// json.Parse round-trips any JsonNode (JsonObject/JsonArray) through JsonElement
// into a native dict.@this / list.@this. So by the time navigation runs, the value
// is a native dict navigated by key (dict.Navigate) — there is no JsonObject left
// to reflect. These tests pin that boundary invariant through the public GetChild
// path (the deleted per-type Dictionary navigator's JsonObject arm was dead code:
// it could only fire on a raw JsonObject, which SetValue prevents).
public class JsonObjectNavigationTests
{
    private static Data MakeData(JsonObject value)
    {
        var d = new Data("trace");
        d.SetValue(value);
        return d;
    }

    [Test]
    public async Task SetValue_JsonObject_BecomesNativeDict_NotRaw()
    {
        var d = MakeData(new JsonObject { ["id"] = "abc123" });
        await Assert.That(d.Peek()).IsTypeOf<global::app.type.dict.@this>();
    }

    [Test]
    public async Task GetChild_TopLevelKey_ReturnsValue()
    {
        var d = MakeData(new JsonObject { ["id"] = "abc123" });
        var result = await d.GetChild("id");
        await Assert.That(result.IsInitialized).IsTrue();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("abc123");
    }

    [Test]
    public async Task GetChild_NestedKey_ReachesViaDotPath()
    {
        // The motivating BuildGoal.goal pattern:
        //   set %trace% = {..., "goal": %goal%, ...}, type=json
        //   save %trace% to file '/.build/traces/%!trace.id%/%trace.goal.name%.json'
        var d = MakeData(new JsonObject { ["goal"] = new JsonObject { ["name"] = "Hello" } });
        var result = await d.GetChild("goal.name");
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Hello");
    }

    [Test]
    public async Task GetChild_CaseInsensitiveKey()
    {
        var d = MakeData(new JsonObject { ["Name"] = "Hello" });
        var result = await d.GetChild("name");
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Hello");
    }

    [Test]
    public async Task GetChild_MissingKey_ReturnsNotFound()
    {
        var d = MakeData(new JsonObject { ["goal"] = "Hello" });
        var result = await d.GetChild("nonexistent");
        await Assert.That(result.IsInitialized).IsFalse();
    }

    [Test]
    public async Task GetChild_Count_ReturnsItemCount()
    {
        var d = MakeData(new JsonObject { ["a"] = 1, ["b"] = 2, ["c"] = 3 });
        var result = await d.GetChild("count");
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("3");
    }

    [Test]
    public async Task RawDictShapes_AlsoNarrowToNativeDict()
    {
        // Regression guard: the canonical IDictionary<string,object?> and non-generic
        // IDictionary (Hashtable) shapes also narrow at the boundary and navigate.
        var d1 = new Data("");
        d1.SetValue(new Dictionary<string, object?> { ["k"] = "v" });
        await Assert.That((await (await d1.GetChild("k")).Value())?.ToString()).IsEqualTo("v");

        var d2 = new Data("");
        d2.SetValue(new System.Collections.Hashtable { ["k"] = "v" });
        await Assert.That((await (await d2.GetChild("k")).Value())?.ToString()).IsEqualTo("v");
    }
}
