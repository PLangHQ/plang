namespace PLang.Tests.App.CompareRedesign;

// Stage 7 — `path`'s interior string-math moves onto the type (OBP smell #5).
// `path.IsUnder(root)` replaces `f.Relative.StartsWith(rootRel)`; `path.Kind`
// replaces `Format.TypeFromExtension(p.Extension)`. Raw `.Relative` /
// `.Extension` become `internal`, feeding the new methods + the `!relative` /
// `!extension` derived projections.
public class Stage7_PathGrowthTests
{
    [Test]
    public async Task PathIsUnder_ReplacesRelativeStartsWith()
    {
        // builder:193-194 — f.Relative.StartsWith(bfRel) becomes f.IsUnder(bfRoot)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task PathKind_ReplacesFormatTypeFromExtension()
    {
        // file/read:79 — Format.TypeFromExtension(p.Extension) becomes p.Kind (owned by path/the format registry)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task PathRelative_NowInternal_NotOnPublicSurface()
    {
        // reflection: path.@this.Relative is internal — public surface is !relative (derived projection)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task PathExtension_NowInternal_PublicViaBangExtension()
    {
        // reflection: path.@this.Extension is internal; public surface is !extension
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
