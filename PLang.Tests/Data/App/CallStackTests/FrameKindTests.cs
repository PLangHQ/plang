using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

/// <summary>
/// One chain of frames; each frame's kind answers what it is. A frame that only binds names (a loop's item, a
/// handler's error) stands where its caller stands — its goal and step are its caller's — but doesn't deepen the
/// chain, isn't timed, and is never a goal's own frame; it is still a place in the chain, among its caller's children.
/// </summary>
public class FrameKindTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    private IEnumerable<Data> Names(string name, object value) => new[] { new Data(name, value, context: Ctx) };

    [Test]
    public async Task ABindingFrame_DoesNotDeepenTheChain_NorCountTowardItsLimit()
    {
        var stack = new Calls(TestCalls.Settings()) { MaxDepth = 2 };
        await using var a = stack.Push(MakeAction(Ctx, "A"));
        await using var item = stack.Push(Names("item", 1));
        await using var b = stack.Push(MakeAction(Ctx, "B"));

        await Assert.That(item.Depth.ToInt32()).IsEqualTo(1);
        await Assert.That(b.Depth.ToInt32()).IsEqualTo(2);
    }

    [Test]
    public async Task ABindingFrame_StandsWhereItsCallerStands_IsAmongItsChildren_AndIsNeverTheScope()
    {
        var stack = new Calls(TestCalls.Settings(new Dictionary<string, object?> { ["timing"] = true }));
        var action = MakeAction(Ctx, "A");
        await using var frame = stack.Push(action);
        await using var item = stack.Push(Names("item", 1));

        await Assert.That(item.Step).IsSameReferenceAs(frame.Step);
        await Assert.That(item.Goal).IsSameReferenceAs(frame.Goal);
        await Assert.That(frame.Children).Contains(item);
        await Assert.That(item.Duration).IsNull();
        await Assert.That(frame.Duration).IsNotNull();
        await Assert.That(stack.Scope).IsNotSameReferenceAs(item);
    }

    [Test]
    public async Task AFrameIsBornWithItsNames_AndReadsOutward()
    {
        var stack = new Calls(TestCalls.Settings());
        await using var outer = stack.Push(Names("city", "Reykjavik"));
        await using var inner = stack.Push(Names("day", "today"));

        await Assert.That(inner.TryGet("city", out var city)).IsTrue();
        await Assert.That((await city.Value())?.ToString()).IsEqualTo("Reykjavik");
        await Assert.That(inner.Holds("city")).IsFalse();
        await Assert.That(inner.Holds("day")).IsTrue();
    }
}
