using app.test;
using Tag = global::app.module.action.test.Tag;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 9 — test.tag action.
/// Declarative metadata: "- set test tag 'http', 'fast'". Parameter Tags: list&lt;text&gt;.
/// Runtime behavior moves the tags into Test.Current.Tags + Data.Ok. The real work
/// (extracting tags from .pr) happens at discovery — see Batch 8. Outside test mode
/// (Current == null), this action no-ops so users can embed test.tag in shared goals.
/// </summary>
public class TagActionTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/test");
    }

    [After(Test)]
    public async Task Teardown() => await _app.DisposeAsync();

    private static global::app.test.@this NewTest() =>
        new(global::PLang.Tests.TestApp.SharedContext)
        {
            Goal = new Goal { Name = "T", Path = global::app.type.item.path.@this.Resolve("/Tests/T.test.goal", global::PLang.Tests.TestApp.SharedContext) }
        };

    private static IEnumerable<string> TagStrings(global::app.test.@this test) =>
        test.Tags.Select(r => r.Peek().Clr<string>() ?? "");

    private global::app.data.@this<global::app.type.item.list.@this> Tags(params string[] tags) =>
        new("Tags", global::PLang.Tests.Shared.Make.List(tags, _app.User.Context));

    // When Test.Current is set (test in flight), test.tag with Tags=["http","fast"]
    // moves both tags into Current.Tags.
    [Test]
    public async Task Tag_InsideTest_WritesToCurrentTags()
    {
        _app.Test.Current = NewTest();

        var result = await new Tag(_app.User.Context) { Tags = Tags("http", "fast") }.Run();

        await result.IsSuccess();
        await _app.Test.Current!.Tags.Contains("http").IsTrue();
        await _app.Test.Current!.Tags.Contains("fast").IsTrue();
    }

    // test.tag always returns Data.Ok; does not write to MemoryStack, does not touch
    // Variables or the test collection. Pure tag-metadata action.
    [Test]
    public async Task Tag_ReturnsDataOk_NoSideEffectsBeyondTags()
    {
        _app.Test.Current = NewTest();
        var beforeResultCount = _app.Test.Count;
        var beforeVarCount = _app.User.Context.Variable.GetNames().Count();

        var result = await new Tag(_app.User.Context) { Tags = Tags("t1") }.Run();

        await result.IsSuccess();
        await Assert.That(_app.Test.Count).IsEqualTo(beforeResultCount);
        await Assert.That(_app.User.Context.Variable.GetNames().Count()).IsEqualTo(beforeVarCount);
    }

    // Current == null (normal plang run, not --test mode) → test.tag is a no-op:
    // returns Data.Ok, does not throw, does not write. Lets users embed test.tag in
    // shared goals without breaking production.
    [Test]
    public async Task Tag_OutsideTest_CurrentNull_NoOpsSafely()
    {
        _app.Test.Current = null;

        var result = await new Tag(_app.User.Context) { Tags = Tags("ignored") }.Run();

        await result.IsSuccess();
        await Assert.That(_app.Test.Current).IsNull();
    }

    // Two test.tag calls: ["http"], then ["fast","slow"], then a duplicate ["http"]
    // → Current.Tags has three DISTINCT tags {"http","fast","slow"} (storage keeps the
    // dup; the report de-dups on display).
    [Test]
    public async Task Tag_MultipleInvocations_TagsAccumulate()
    {
        _app.Test.Current = NewTest();

        await new Tag(_app.User.Context) { Tags = Tags("http") }.Run();
        await new Tag(_app.User.Context) { Tags = Tags("fast", "slow") }.Run();
        await new Tag(_app.User.Context) { Tags = Tags("http") }.Run(); // duplicate

        var distinct = TagStrings(_app.Test.Current!).Distinct().ToList();
        await Assert.That(distinct.Count).IsEqualTo(3);
        await Assert.That(distinct.Contains("http")).IsTrue();
        await Assert.That(distinct.Contains("fast")).IsTrue();
        await Assert.That(distinct.Contains("slow")).IsTrue();
    }
}
