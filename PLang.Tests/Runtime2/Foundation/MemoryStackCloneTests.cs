using PLang.Runtime2.Memory;

namespace PLang.Tests.Runtime2.Foundation;

/// <summary>
/// Proves Fix #7: MemoryStack.Clone() is shallow — mutable values are shared by reference.
/// These tests assert CORRECT behavior (deep clone isolation).
/// Before the fix they FAIL, proving the bug. After the fix they PASS.
/// </summary>
public class MemoryStackCloneTests
{
    [Test]
    public async Task Clone_ListValue_IsIsolatedFromOriginal()
    {
        var ms = new MemoryStack();
        var list = new List<string> { "a", "b" };
        ms.Set("items", list);

        var clone = ms.Clone();

        // Mutate via the clone
        var cloneList = clone.Get<List<string>>("items");
        cloneList!.Add("c");

        // Original should NOT be affected
        var originalList = ms.Get<List<string>>("items");
        await Assert.That(originalList!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Clone_DictionaryValue_IsIsolatedFromOriginal()
    {
        var ms = new MemoryStack();
        var dict = new Dictionary<string, object?> { ["key1"] = "val1" };
        ms.Set("config", dict);

        var clone = ms.Clone();

        // Mutate via the clone
        var cloneDict = clone.Get<Dictionary<string, object?>>("config");
        cloneDict!["key2"] = "val2";

        // Original should NOT be affected
        var originalDict = ms.Get<Dictionary<string, object?>>("config");
        await Assert.That(originalDict!.ContainsKey("key2")).IsFalse();
    }

    [Test]
    public async Task Clone_NestedListInDict_IsIsolatedFromOriginal()
    {
        var ms = new MemoryStack();
        var data = new Dictionary<string, object?>
        {
            ["tags"] = new List<string> { "alpha", "beta" }
        };
        ms.Set("record", data);

        var clone = ms.Clone();

        // Navigate into the clone and mutate the nested list
        var cloneData = clone.Get<Dictionary<string, object?>>("record");
        var cloneTags = (List<string>)cloneData!["tags"]!;
        cloneTags.Add("gamma");

        // Original's nested list should NOT be affected
        var originalData = ms.Get<Dictionary<string, object?>>("record");
        var originalTags = (List<string>)originalData!["tags"]!;
        await Assert.That(originalTags.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Clone_ScalarValue_RemainsIndependent()
    {
        // Scalars (strings, ints) are immutable — clone should always work for these
        var ms = new MemoryStack();
        ms.Set("count", 42);
        ms.Set("name", "original");

        var clone = ms.Clone();
        clone.Set("count", 99);
        clone.Set("name", "modified");

        var originalCount = ms.Get<long>("count");
        var originalName = ms.Get<string>("name");
        await Assert.That(originalCount).IsEqualTo(42);
        await Assert.That(originalName).IsEqualTo("original");
    }
}
