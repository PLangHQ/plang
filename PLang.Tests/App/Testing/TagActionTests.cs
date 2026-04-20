namespace PLang.Tests.App.Testing;

/// <summary>
/// Batch 9 — test.tag action.
/// Declarative metadata: "- set test tag 'http', 'fast'". Parameter Tags: string[].
/// Runtime behavior is a thin write to Testing.CurrentTest.UserTags + Data.Ok.
/// The real work (extracting tags from .pr) happens at discovery time — see Batch 8.
/// Outside test mode (CurrentTest == null), this action no-ops so users can embed
/// test.tag in shared goals without breaking production runs.
/// </summary>
public class TagActionTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/test");
    }

    // When Testing.CurrentTest is set (test in flight), test.tag with Tags=["http","fast"]
    // writes both tags into CurrentTest.UserTags.
    [Test]
    public async Task Tag_InsideTest_WritesToCurrentTestUserTags()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // test.tag always returns Data.Ok; does not write to MemoryStack, does not touch
    // Variables or Results. Pure tag-metadata action.
    [Test]
    public async Task Tag_ReturnsDataOk_NoSideEffectsBeyondTags()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Testing.CurrentTest == null (normal plang run, not --test mode) → test.tag is a
    // no-op: returns Data.Ok, does not throw, does not write anywhere. Lets users
    // embed test.tag in shared goals without breaking production. (independent)
    [Test]
    public async Task Tag_OutsideTest_CurrentTestNull_NoOpsSafely()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Two test.tag calls: ["http"], then ["fast","slow"] → CurrentTest.UserTags
    // contains {"http","fast","slow"}. Set semantics — duplicate adds are idempotent,
    // order not preserved.
    [Test]
    public async Task Tag_MultipleInvocations_TagsAccumulate()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
