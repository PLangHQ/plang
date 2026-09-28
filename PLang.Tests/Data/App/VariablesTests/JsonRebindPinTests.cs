namespace PLang.Tests.App.VariablesTests;

using Variables = global::app.type.item.variable.list.@this;

/// <summary>
/// Pin test (architect acceptance, stage1-navigation-write-answer.md): a write into an immutable
/// json host materialises it one level into a mutable dict + sets the key (json kind's Set). This
/// captures TODAY's one-level-rebind behavior — it is not a new design, just guarded against drift.
/// </summary>
public class JsonRebindPinTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create(
        "/tmp/json-rebind-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Set_OneLevelIntoJsonHost_MaterialisesAndSetsKey()
    {
        var stack = _app.User.Context.Variable;

        // A clr(json) object value under %j% — an immutable JsonElement host.
        using var doc = System.Text.Json.JsonDocument.Parse("""{"a":"one","b":"two"}""");
        var j = new global::app.data.@this("j",
            _app.User.Context.App.type.list[new global::app.type.@this("item", "json"), _app.User.Context]
                .Make(doc.RootElement.Clone(), _app.User.Context),
            context: _app.User.Context);
        await stack.Set("j", j);

        // One-level deep write — json host materialises into a dict, sets the key.
        await new global::app.type.item.variable.@this("j.a").Set("ONE", stack.Context);

        var a = await new global::app.type.item.variable.@this("j.a").Start(stack.Context);
        await Assert.That((await a!.Value())?.ToString()).IsEqualTo("ONE");

        // The sibling survives the materialisation (json content became the dict's keys).
        var b = await new global::app.type.item.variable.@this("j.b").Start(stack.Context);
        await Assert.That((await b!.Value())?.ToString()).IsEqualTo("two");
    }
}
