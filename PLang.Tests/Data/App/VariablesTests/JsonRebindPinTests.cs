namespace PLang.Tests.App.VariablesTests;

using Variables = global::app.variable.list.@this;

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
            global::app.type.@this.Create("object", "json", context: _app.User.Context)
                .Create(doc.RootElement.Clone(), _app.User.Context),
            context: _app.User.Context);
        await stack.Set("j", j);

        // One-level deep write — json host materialises into a dict, sets the key.
        await stack.Set("j.a", "ONE");

        var a = await stack.Get("j.a");
        await Assert.That((await a!.Value())?.ToString()).IsEqualTo("ONE");

        // The sibling survives the materialisation (json content became the dict's keys).
        var b = await stack.Get("j.b");
        await Assert.That((await b!.Value())?.ToString()).IsEqualTo("two");
    }
}
