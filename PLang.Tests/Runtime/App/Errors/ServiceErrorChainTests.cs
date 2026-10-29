using app.error;
using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.Errors;

public class ServiceErrorChainTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task ServiceError_HoldsTheCallItFailedIn()
    {
        var stack = TestCalls.Stack(app, TestCalls.Settings());
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "Outer"), await stack.Setting());
        await using var failing = stack.Push(MakeAction(app.actor.list.User.Context, "Failing"), await stack.Setting());
        var sv = new ServiceError("crash") { call = failing };
        await Assert.That(sv.call).IsEqualTo(failing);
    }

    [Test]
    public async Task ServiceError_ItsCallsChainReachesTheRoot()
    {
        var stack = TestCalls.Stack(app, TestCalls.Settings());
        await using var root = stack.Push(MakeAction(app.actor.list.User.Context, "Root"), await stack.Setting());
        await using var middle = stack.Push(MakeAction(app.actor.list.User.Context, "Middle"), await stack.Setting());
        await using var leaf = stack.Push(MakeAction(app.actor.list.User.Context, "Leaf"), await stack.Setting());
        var sv = new ServiceError("crash") { call = leaf };
        await Assert.That(sv.call!.list.Count).IsEqualTo(3);
        await Assert.That(sv.call!.list[2]).IsEqualTo(root);
    }

    // An error born about an action's step (a resolve run with no frame of its own) names that step, not the frame
    // current when it is recorded.
    [Test]
    public async Task AResolveError_RecordedUnderAnotherFrame_NamesItsStep()
    {
        var context = app.actor.list.User.Context;
        var built = global::PLang.Tests.Shared.Make.Goal(context, "Start", "/Start.goal",
            global::PLang.Tests.Shared.Make.Step("set x", global::PLang.Tests.Shared.Make.Action(context, "variable", "set",
                global::PLang.Tests.Shared.Make.Param(context, "Name", "x", "variable"), ("Value", 1))),
            global::PLang.Tests.Shared.Make.Step("set nothing", global::PLang.Tests.Shared.Make.Action(context, "variable", "set", ("Value", 2))));
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, built);
        var unnamed = goal.Step[1].Code[0];

        global::app.error.@this error;
        await using (var other = context.call.Push(goal.Step[0].Code[0], await context.call.Setting(), context.Variable))
        {
            error = (await unnamed.Bind(context)).Error!;
            other.Record(error, context);
        }

        await Assert.That(error.Key).IsEqualTo("MissingRequiredProperty");
        await Assert.That(error.call).IsNotNull();
        await Assert.That(error.Step).IsEqualTo(goal.Step[1]);
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
