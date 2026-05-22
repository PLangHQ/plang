using app.variables;

namespace PLang.Tests.App.Foundation;

/// <summary>
/// Proves Fix #7: Variables.Clone() is shallow — mutable values are shared by reference.
/// These tests assert CORRECT behavior (deep clone isolation).
/// Before the fix they FAIL, proving the bug. After the fix they PASS.
/// </summary>
public class VariablesCloneTests
{
    [Test]
    public async Task Clone_ListValue_IsIsolatedFromOriginal()
    {
        var vars = new Variables();
        var list = new List<string> { "a", "b" };
        vars.Set("items", list);

        var clone = vars.Clone();

        // Mutate via the clone
        var cloneList = clone.Get<List<string>>("items");
        cloneList!.Add("c");

        // Original should NOT be affected
        var originalList = vars.Get<List<string>>("items");
        await Assert.That(originalList!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Clone_DictionaryValue_IsIsolatedFromOriginal()
    {
        var vars = new Variables();
        var dict = new Dictionary<string, object?> { ["key1"] = "val1" };
        vars.Set("config", dict);

        var clone = vars.Clone();

        // Mutate via the clone
        var cloneDict = clone.Get<Dictionary<string, object?>>("config");
        cloneDict!["key2"] = "val2";

        // Original should NOT be affected
        var originalDict = vars.Get<Dictionary<string, object?>>("config");
        await Assert.That(originalDict!.ContainsKey("key2")).IsFalse();
    }

    [Test]
    public async Task Clone_NestedListInDict_IsIsolatedFromOriginal()
    {
        var vars = new Variables();
        var data = new Dictionary<string, object?>
        {
            ["tags"] = new List<string> { "alpha", "beta" }
        };
        vars.Set("record", data);

        var clone = vars.Clone();

        // Navigate into the clone and mutate the nested list
        var cloneData = clone.Get<Dictionary<string, object?>>("record");
        var cloneTags = (List<string>)cloneData!["tags"]!;
        cloneTags.Add("gamma");

        // Original's nested list should NOT be affected
        var originalData = vars.Get<Dictionary<string, object?>>("record");
        var originalTags = (List<string>)originalData!["tags"]!;
        await Assert.That(originalTags.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Clone_ScalarValue_RemainsIndependent()
    {
        // Scalars (strings, ints) are immutable — clone should always work for these
        var vars = new Variables();
        vars.Set("count", 42);
        vars.Set("name", "original");

        var clone = vars.Clone();
        clone.Set("count", 99);
        clone.Set("name", "modified");

        var originalCount = vars.Get<long>("count");
        var originalName = vars.Get<string>("name");
        await Assert.That(originalCount).IsEqualTo(42);
        await Assert.That(originalName).IsEqualTo("original");
    }
}
