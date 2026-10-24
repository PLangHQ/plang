namespace PLang.Tests.App.TypedReturnsTests;

// Stage 4 (kind-is-type, decision 792): a Data of `{item, kind: json}` holding raw bytes
// materializes on first property access; a parsed json number is a plang number.
public class JsonPropertyAccessMaterializesTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    [Test]
    public async Task PropertyAccess_OnItemOfKindJson_YieldsPlangNumber_ParsesOnce()
    {
        var src = new Data("x", "{\"a\":1}", new global::app.type.@this("item", "json"), context: _app.actor.list.User.Context);

        var aValue = await src.Get("a");

        await Assert.That(aValue.IsInitialized).IsTrue();
        var materialized = await aValue.Value();
        // A parsed json number is a plang number (not the text "1").
        await Assert.That(materialized).IsTypeOf<global::app.type.item.number.@this>();
        await Assert.That(materialized!.ToString()).IsEqualTo("1");
        await Assert.That(src.MaterializeCount()).IsEqualTo(1);
    }
}
