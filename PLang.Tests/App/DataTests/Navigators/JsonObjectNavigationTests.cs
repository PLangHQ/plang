using global::app.variables.navigators;
using System.Text.Json.Nodes;

namespace PLang.Tests.App.DataTests.Navigators;

// JsonObject implements IDictionary<string, JsonNode?> (NOT IDictionary<string, object?>
// and NOT non-generic IDictionary), so the canonical-shape arms in global::app.variables.navigators.Dictionary
// don't see it. Without the generic IDictionary<string, T> arm, dot-path navigation
// through `set ... type=json` values fell through to the reflection navigator and
// surfaced only the JsonObject CLR properties (Count, Options, Parent, Root) — never
// the actual JSON keys. These tests pin the third arm.
public class JsonObjectNavigationTests
{
    private static Data MakeData(JsonObject value)
    {
        var d = new Data("trace");
        d.Value = value;
        return d;
    }

    [Test]
    public async Task CanNavigate_JsonObject_ReturnsTrue()
    {
        var nav = new global::app.variables.navigators.Dictionary();
        var jo = new JsonObject { ["goal"] = new JsonObject { ["name"] = "Hello" } };
        await Assert.That(nav.CanNavigate(MakeData(jo))).IsTrue();
    }

    [Test]
    public async Task Navigate_TopLevelKey_ReturnsValue()
    {
        var nav = new global::app.variables.navigators.Dictionary();
        var jo = new JsonObject { ["id"] = "abc123" };
        var result = nav.Navigate(MakeData(jo), "id");
        await Assert.That(result.IsInitialized).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("abc123");
    }

    [Test]
    public async Task Navigate_NestedKey_ReachesViaDotPath()
    {
        // The motivating BuildGoal.goal pattern:
        //   set %trace% = {..., "goal": %goal%, ...}, type=json
        //   save %trace% to file '/.build/traces/%!trace.id%/%trace.goal.name%.json'
        // %trace.goal.name% must reach into the inner JsonObject — without the
        // generic-dict arm the lookup landed on JsonObject's CLR Root prop instead.
        var jo = new JsonObject
        {
            ["goal"] = new JsonObject { ["name"] = "Hello" }
        };
        var data = MakeData(jo);
        // Simulate dot-path: Navigate to "goal" then to "name".
        var nav = new global::app.variables.navigators.Dictionary();
        var goalResult = nav.Navigate(data, "goal");
        await Assert.That(goalResult.IsInitialized).IsTrue();

        var goalData = new Data("goal");
        goalData.Value = goalResult.Value;
        var nameResult = nav.Navigate(goalData, "name");
        await Assert.That(nameResult.Value?.ToString()).IsEqualTo("Hello");
    }

    [Test]
    public async Task Navigate_CaseInsensitiveKey()
    {
        var nav = new global::app.variables.navigators.Dictionary();
        var jo = new JsonObject { ["Name"] = "Hello" };
        var result = nav.Navigate(MakeData(jo), "name");
        await Assert.That(result.Value?.ToString()).IsEqualTo("Hello");
    }

    [Test]
    public async Task Navigate_MissingKey_ReturnsNotFound()
    {
        var nav = new global::app.variables.navigators.Dictionary();
        var jo = new JsonObject { ["goal"] = "Hello" };
        var result = nav.Navigate(MakeData(jo), "nonexistent");
        await Assert.That(result.IsInitialized).IsFalse();
    }

    [Test]
    public async Task Navigate_Count_ReturnsItemCount()
    {
        var nav = new global::app.variables.navigators.Dictionary();
        var jo = new JsonObject { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
        var result = nav.Navigate(MakeData(jo), "Count");
        await Assert.That(result.Value).IsEqualTo(3);
    }

    [Test]
    public async Task Navigate_OriginalShapes_StillWork()
    {
        // Regression guard: the prior IDictionary<string,object?> and non-generic
        // IDictionary paths must not have been broken by the third arm.
        var nav = new global::app.variables.navigators.Dictionary();

        var canonical = new Dictionary<string, object?> { ["k"] = "v" };
        var d1 = new Data("");
        d1.Value = canonical;
        await Assert.That(nav.Navigate(d1, "k").Value).IsEqualTo("v");

        var legacy = new System.Collections.Hashtable { ["k"] = "v" };
        var d2 = new Data("");
        d2.Value = legacy;
        await Assert.That(nav.Navigate(d2, "k").Value).IsEqualTo("v");
    }
}
