namespace PLang.Tests.App.TypeKindStrict.ItemTypeTests;

// Stage 4 (kind-is-type, decision 792): a json array is `{item, kind: json}` holding raw text;
// EnumerateItems yields plang numbers, and after the read the value narrows to a plang list (787).
public class JsonArrayForeachTests
{
    [Test] public async Task Foreach_OverAJsonArray_YieldsPlangNumbers_AndNarrowsToList()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        var data = new global::app.data.@this("x", "[1, 2, 3]", new global::app.type.@this("item", "json"), context: ctx);

        var items = new List<global::app.type.item.@this?>();
        foreach (var (_, item) in await data.EnumerateItems())
            items.Add(await item.Value());

        await Assert.That(items.Count).IsEqualTo(3);
        foreach (var it in items)
            await Assert.That(it).IsTypeOf<global::app.type.item.number.@this>();
        await Assert.That(items.Select(i => i?.ToString()).ToList()).IsEquivalentTo(new[] { "1", "2", "3" });

        // After the read the value is a plang list (decision 787).
        await Assert.That(data.Type.Name).IsEqualTo("list");
    }
}
