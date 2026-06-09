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
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task GoldenDiff_StillProducesSameDiffTrees_ForKnownCases()
    {
        // pick a representative case from the v1 DataCompareTests and assert Diff produces the same shape
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
