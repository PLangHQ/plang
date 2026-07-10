namespace PLang.Tests.App.CompareRedesign;

// Stage 6 — the golden-diff `data.Compare` (`this.Compare.cs`) renames to
// `Diff` (`this.Diff.cs`). ~14 test call sites migrate; no production callers.
// The diff trees produced are unchanged — only the name.
public class Stage6_DiffRenameTests
{
    [Test]
    public async Task GoldenDiff_RenamedFromCompareToDiff_FileAndMethod()
    {
        // this.Compare.cs → this.Diff.cs; reflection: Data.Diff(other) exists, Data.Compare(other) is the new Comparison-returning method
        // Data.Diff(other) is the golden-diff; Data.Compare(other) is the Comparison entry.
        var diff = typeof(Data).GetMethod("Diff", new[] { typeof(Data) });
        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!.ReturnType).IsEqualTo(typeof(System.Threading.Tasks.ValueTask<global::app.data.@this>));
        var compare = typeof(Data).GetMethod("Compare", new[] { typeof(Data) });
        await Assert.That(compare).IsNotNull();
        await Assert.That(compare!.ReturnType).IsEqualTo(typeof(System.Threading.Tasks.ValueTask<global::app.data.Comparison>));
    }

    [Test]
    public async Task GoldenDiff_StillProducesSameDiffTrees_ForKnownCases()
    {
        // pick a representative case from the v1 DataCompareTests and assert Diff produces the same shape
        var a = new Data("a", "hello", context: global::PLang.Tests.TestApp.SharedContext);
        var b = new Data("b", "hello", context: global::PLang.Tests.TestApp.SharedContext);
        var result = await a.Diff(b);
        var tree = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());
        await Assert.That(tree).IsNotNull();
        await Assert.That(tree!["match"]).IsEqualTo(true);
        var c = new Data("c", "different", context: global::PLang.Tests.TestApp.SharedContext);
        var tree2 = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await (await a.Diff(c)).Value());
        await Assert.That(tree2!["match"]).IsEqualTo(false);
    }
}
