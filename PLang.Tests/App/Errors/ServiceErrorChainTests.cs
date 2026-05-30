using app.error;
using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.Errors;

public class ServiceErrorChainTests
{
    [Test]
    public async Task ServiceError_CallFrames_TypedAsReadOnlyListOfCall()
    {
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        var chain = call.SnapshotChain();
        var sv = new ServiceError("crash", call.Action.Step!, chain);
        IReadOnlyList<global::app.callstack.call.@this> typed = sv.CallFrames;
        await Assert.That(typed).IsNotNull();
    }

    [Test]
    public async Task ServiceError_ChainIndexZero_IsFailingCall()
    {
        var stack = new CallStack();
        await using var outer = stack.Push(MakeAction("Outer"));
        await using var failing = stack.Push(MakeAction("Failing"));
        var chain = failing.SnapshotChain();
        var sv = new ServiceError("crash", failing.Action.Step!, chain);
        await Assert.That(sv.CallFrames[0]).IsEqualTo(failing);
    }

    [Test]
    public async Task ServiceError_ChainWalksCallerToRoot()
    {
        var stack = new CallStack();
        await using var root = stack.Push(MakeAction("Root"));
        await using var middle = stack.Push(MakeAction("Middle"));
        await using var leaf = stack.Push(MakeAction("Leaf"));
        var chain = leaf.SnapshotChain();
        var sv = new ServiceError("crash", leaf.Action.Step!, chain);
        await Assert.That(sv.CallFrames.Count).IsEqualTo(3);
        await Assert.That(sv.CallFrames[2]).IsEqualTo(root);
    }

    [Test]
    public async Task ServiceError_ParamsCarriedFromHandlerSnapshot()
    {
        var sv = new ServiceError("crash", new Step { Index = 0, Text = "test" });
        sv.Params = new List<ParamSnapshot>
        {
            new ParamSnapshot { Name = "x", PrValue = "1", FinalValue = 1, WasAccessed = true }
        };
        await Assert.That(sv.Params).IsNotNull();
        await Assert.That(sv.Params!.Count).IsEqualTo(1);
        await Assert.That(sv.Params[0].Name).IsEqualTo("x");
    }
}
