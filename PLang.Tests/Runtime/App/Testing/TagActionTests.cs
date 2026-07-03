using app.tester;
using Tag = global::app.module.test.Tag;

namespace PLang.Tests.App.Tester;

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
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/test");
    }

    [After(Test)]
    public async Task Teardown() => await _app.DisposeAsync();

    private static global::app.tester.Run NewRun() =>
        new(new global::app.tester.test.@this { Goal = new Goal { Name = "T", Path = global::app.type.path.@this.Resolve("/Tests/T.test.goal", global::PLang.Tests.TestApp.SharedContext) } });

    // When Testing.CurrentTest is set (test in flight), test.tag with Tags=["http","fast"]
    // writes both tags into CurrentTest.UserTags.
    [Test]
    public async Task Tag_InsideTest_WritesToCurrentTestUserTags()
    {
        _app.Tester.CurrentTest = NewRun();

        var action = new Tag(_app.User.Context) { Tags = new global::app.data.@this<global::app.type.list.@this>("Tags", global::app.type.list.@this.FromRaw(new[] { "http", "fast" }, _app.User.Context))
        };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_app.Tester.CurrentTest.UserTags.Contains("http")).IsTrue();
        await Assert.That(_app.Tester.CurrentTest.UserTags.Contains("fast")).IsTrue();
    }

    // test.tag always returns Data.Ok; does not write to MemoryStack, does not touch
    // Variables or Results. Pure tag-metadata action.
    [Test]
    public async Task Tag_ReturnsDataOk_NoSideEffectsBeyondTags()
    {
        _app.Tester.CurrentTest = NewRun();
        var beforeResultCount = _app.Tester.Results.Count;
        var beforeVarCount = _app.User.Context.Variable.GetNames().Count();

        var action = new Tag(_app.User.Context) { Tags = new global::app.data.@this<global::app.type.list.@this>("Tags", global::app.type.list.@this.FromRaw(new[] { "t1" }, _app.User.Context))
        };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_app.Tester.Results.Count).IsEqualTo(beforeResultCount);
        await Assert.That(_app.User.Context.Variable.GetNames().Count()).IsEqualTo(beforeVarCount);
    }

    // Testing.CurrentTest == null (normal plang run, not --test mode) → test.tag is a
    // no-op: returns Data.Ok, does not throw, does not write anywhere. Lets users
    // embed test.tag in shared goals without breaking production. (independent)
    [Test]
    public async Task Tag_OutsideTest_CurrentTestNull_NoOpsSafely()
    {
        _app.Tester.CurrentTest = null;

        var action = new Tag(_app.User.Context) { Tags = new global::app.data.@this<global::app.type.list.@this>("Tags", global::app.type.list.@this.FromRaw(new[] { "ignored" }, _app.User.Context))
        };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_app.Tester.CurrentTest).IsNull();
    }

    // Two test.tag calls: ["http"], then ["fast","slow"] → CurrentTest.UserTags
    // contains {"http","fast","slow"}. Set semantics — duplicate adds are idempotent,
    // order not preserved.
    [Test]
    public async Task Tag_MultipleInvocations_TagsAccumulate()
    {
        _app.Tester.CurrentTest = NewRun();

        await new Tag(_app.User.Context) { Tags = new global::app.data.@this<global::app.type.list.@this>("Tags", global::app.type.list.@this.FromRaw(new[] { "http" }, _app.User.Context))
        }.Run();
        await new Tag(_app.User.Context) { Tags = new global::app.data.@this<global::app.type.list.@this>("Tags", global::app.type.list.@this.FromRaw(new[] { "fast", "slow" }, _app.User.Context))
        }.Run();
        await new Tag(_app.User.Context) { Tags = new global::app.data.@this<global::app.type.list.@this>("Tags", global::app.type.list.@this.FromRaw(new[] { "http" }, _app.User.Context)) // duplicate
        }.Run();

        await Assert.That(_app.Tester.CurrentTest.UserTags.Count).IsEqualTo(3);
        await Assert.That(_app.Tester.CurrentTest.UserTags.Contains("http")).IsTrue();
        await Assert.That(_app.Tester.CurrentTest.UserTags.Contains("fast")).IsTrue();
        await Assert.That(_app.Tester.CurrentTest.UserTags.Contains("slow")).IsTrue();
    }
}
