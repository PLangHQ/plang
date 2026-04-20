using global::App.Test;
using Tag = global::App.modules.test.Tag;

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

    [After(Test)]
    public async Task Teardown() => await _app.DisposeAsync();

    private static TestRun NewRun() =>
        new(new TestFile { Path = "Tests/T.test.goal", EntryGoalName = "T" });

    // When Testing.CurrentTest is set (test in flight), test.tag with Tags=["http","fast"]
    // writes both tags into CurrentTest.UserTags.
    [Test]
    public async Task Tag_InsideTest_WritesToCurrentTestUserTags()
    {
        _app.Testing.CurrentTest = NewRun();

        var action = new Tag
        {
            Context = _app.User.Context,
            Tags = new global::App.Data.@this<string[]>("Tags", new[] { "http", "fast" })
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_app.Testing.CurrentTest.UserTags.Contains("http")).IsTrue();
        await Assert.That(_app.Testing.CurrentTest.UserTags.Contains("fast")).IsTrue();
    }

    // test.tag always returns Data.Ok; does not write to MemoryStack, does not touch
    // Variables or Results. Pure tag-metadata action.
    [Test]
    public async Task Tag_ReturnsDataOk_NoSideEffectsBeyondTags()
    {
        _app.Testing.CurrentTest = NewRun();
        var beforeResultCount = _app.Testing.Results.Count;
        var beforeVarCount = _app.User.Context.Variables.GetNames().Count();

        var action = new Tag
        {
            Context = _app.User.Context,
            Tags = new global::App.Data.@this<string[]>("Tags", new[] { "t1" })
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_app.Testing.Results.Count).IsEqualTo(beforeResultCount);
        await Assert.That(_app.User.Context.Variables.GetNames().Count()).IsEqualTo(beforeVarCount);
    }

    // Testing.CurrentTest == null (normal plang run, not --test mode) → test.tag is a
    // no-op: returns Data.Ok, does not throw, does not write anywhere. Lets users
    // embed test.tag in shared goals without breaking production. (independent)
    [Test]
    public async Task Tag_OutsideTest_CurrentTestNull_NoOpsSafely()
    {
        _app.Testing.CurrentTest = null;

        var action = new Tag
        {
            Context = _app.User.Context,
            Tags = new global::App.Data.@this<string[]>("Tags", new[] { "ignored" })
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_app.Testing.CurrentTest).IsNull();
    }

    // Two test.tag calls: ["http"], then ["fast","slow"] → CurrentTest.UserTags
    // contains {"http","fast","slow"}. Set semantics — duplicate adds are idempotent,
    // order not preserved.
    [Test]
    public async Task Tag_MultipleInvocations_TagsAccumulate()
    {
        _app.Testing.CurrentTest = NewRun();

        await new Tag
        {
            Context = _app.User.Context,
            Tags = new global::App.Data.@this<string[]>("Tags", new[] { "http" })
        }.Run();
        await new Tag
        {
            Context = _app.User.Context,
            Tags = new global::App.Data.@this<string[]>("Tags", new[] { "fast", "slow" })
        }.Run();
        await new Tag
        {
            Context = _app.User.Context,
            Tags = new global::App.Data.@this<string[]>("Tags", new[] { "http" }) // duplicate
        }.Run();

        await Assert.That(_app.Testing.CurrentTest.UserTags.Count).IsEqualTo(3);
        await Assert.That(_app.Testing.CurrentTest.UserTags.Contains("http")).IsTrue();
        await Assert.That(_app.Testing.CurrentTest.UserTags.Contains("fast")).IsTrue();
        await Assert.That(_app.Testing.CurrentTest.UserTags.Contains("slow")).IsTrue();
    }
}
