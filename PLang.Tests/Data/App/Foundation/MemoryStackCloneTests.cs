using app.variable;

namespace PLang.Tests.App.Foundation;

/// <summary>
/// Variables.Clone() copies the bindings, so rebinding a name in the clone must
/// not reach back into the original. Container values are native (list.@this /
/// dict.@this) and immutable — a fresh value is bound under the name rather than
/// mutated in place, so isolation is per-binding.
/// </summary>
public class VariablesCloneTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create(
        "/tmp/VariablesCloneTests-" + System.Guid.NewGuid().ToString("N")[..6]);

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task Clone_ListValue_IsIsolatedFromOriginal()
    {
        var vars = new Variables(app.User.Context);
        vars.Set("items", new List<object?> { "a", "b" });

        var clone = vars.Clone();
        clone.Set("items", new List<object?> { "a", "b", "c" });

        var originalList = (await vars.GetValue("items")) as global::app.type.item.list.@this;
        await Assert.That(originalList!.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Clone_DictionaryValue_IsIsolatedFromOriginal()
    {
        var vars = new Variables(app.User.Context);
        vars.Set("config", new Dictionary<string, object?> { ["key1"] = "val1" });

        var clone = vars.Clone();
        clone.Set("config", new Dictionary<string, object?> { ["key1"] = "val1", ["key2"] = "val2" });

        var originalDict = (await vars.GetValue("config")) as global::app.type.item.dict.@this;
        await Assert.That(originalDict!.Has("key2")).IsFalse();
    }

    [Test]
    public async Task Clone_NestedListInDict_IsIsolatedFromOriginal()
    {
        var vars = new Variables(app.User.Context);
        vars.Set("record", new Dictionary<string, object?>
        {
            ["tags"] = new List<string> { "alpha", "beta" }
        });

        var clone = vars.Clone();
        clone.Set("record", new Dictionary<string, object?>
        {
            ["tags"] = new List<string> { "alpha", "beta", "gamma" }
        });

        var originalDict = (await vars.GetValue("record")) as global::app.type.item.dict.@this;
        var originalTags = originalDict!.Get("tags")!.Peek() as global::app.type.item.list.@this;
        await Assert.That(originalTags!.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Clone_ScalarValue_RemainsIndependent()
    {
        var vars = new Variables(app.User.Context);
        vars.Set("count", 42);
        vars.Set("name", "original");

        var clone = vars.Clone();
        clone.Set("count", 99);
        clone.Set("name", "modified");

        // 42 fits in int — numbers stay int and escalate to long only on overflow.
        var originalCount = Convert.ToInt64((await vars.GetValue("count"))!);
        var originalName = (string?)await vars.GetValue("name");
        await Assert.That(originalCount).IsEqualTo(42L);
        await Assert.That(originalName).IsEqualTo("original");
    }
}
