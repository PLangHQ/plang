using app.error;
using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.Errors;

public class ServiceErrorChainTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task ServiceError_CallFrames_TypedAsReadOnlyListOfCall()
    {
        var stack = TestCalls.Stack(app, TestCalls.Settings());
        await using var call = stack.Push(MakeAction(app.actor.list.User.Context, "A"), await stack.Setting());
        var chain = call.Chain;
        var sv = new ServiceError("crash", call.Action.Step!, chain);
        IReadOnlyList<global::app.call.@this> typed = sv.CallFrames;
        await Assert.That(typed).IsNotNull();
    }

    [Test]
    public async Task ServiceError_ChainIndexZero_IsFailingCall()
    {
        var stack = TestCalls.Stack(app, TestCalls.Settings());
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "Outer"), await stack.Setting());
        await using var failing = stack.Push(MakeAction(app.actor.list.User.Context, "Failing"), await stack.Setting());
        var chain = failing.Chain;
        var sv = new ServiceError("crash", failing.Action.Step!, chain);
        await Assert.That(sv.CallFrames[0]).IsEqualTo(failing);
    }

    [Test]
    public async Task ServiceError_ChainWalksCallerToRoot()
    {
        var stack = TestCalls.Stack(app, TestCalls.Settings());
        await using var root = stack.Push(MakeAction(app.actor.list.User.Context, "Root"), await stack.Setting());
        await using var middle = stack.Push(MakeAction(app.actor.list.User.Context, "Middle"), await stack.Setting());
        await using var leaf = stack.Push(MakeAction(app.actor.list.User.Context, "Leaf"), await stack.Setting());
        var chain = leaf.Chain;
        var sv = new ServiceError("crash", leaf.Action.Step!, chain);
        await Assert.That(sv.CallFrames.Count).IsEqualTo(3);
        await Assert.That(sv.CallFrames[2]).IsEqualTo(root);
    }

    [Test]
    public async Task ServiceError_ParamsCarriedFromHandlerSnapshot()
    {
        var sv = new ServiceError("crash", new Step(app.type.list) { Index = 0, Text = "test" });
        sv.Params = new List<ParamSnapshot>
        {
            new ParamSnapshot { Name = "x", PrValue = "1", FinalValue = 1, WasAccessed = true }
        };
        await Assert.That(sv.Params).IsNotNull();
        await Assert.That(sv.Params!.Count).IsEqualTo(1);
        await Assert.That(sv.Params[0].Name).IsEqualTo("x");
    }
}
