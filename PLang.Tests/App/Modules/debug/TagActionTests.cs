namespace PLang.Tests.App.Modules.debug;

// New PLang `tag` action under PLang/App/modules/debug/tag.cs.
// Two input shapes: Pairs (Dict<string,string>) or Label (bare string).
// No-op when CallStack.Current is null.
public class TagActionTests
{
    [Test]
    public async Task Tag_PairsForm_MergesIntoCurrentTags()
    {
        // Action with Pairs={"k1":"v1","k2":"v2"} → Current.Tags contains both entries.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Tag_LabelForm_SetsTagsLabelTrue()
    {
        // Action with Label="manual-checkpoint" → Current.Tags["manual-checkpoint"] == "true".
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Tag_NoOpWhenCurrentNull()
    {
        // CallStack.Current null → handler returns Data.Ok() without throwing.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Tag_AllocatesTagsDict_WhenNull()
    {
        // Current.Tags initially null → first tag write allocates the dict.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Tag_ActionIsNotCacheable()
    {
        // Tag action is observability — Cacheable must be false.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
