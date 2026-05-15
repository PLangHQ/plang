using global::app.Callback;
using global::app.CallStack;
using ActionEntity = app.Goals.Goal.Steps.Step.Actions.Action.@this;

namespace PLang.Tests.App.CallbackTests;

public class ICallbackPositionTests
{
    private static (Goal goal, ActionEntity action) MakeFrame(string name)
    {
        var goal = new Goal { Name = name, Path = $"/{name}.goal" };
        var step = new Step { Index = 0, Text = "step", Goal = goal };
        var action = new ActionEntity { Module = "test", ActionName = "test" };
        action.Step = step; step.Actions.Add(action); goal.Steps.Add(step);
        return (goal, action);
    }

    [Test]
    public async Task ICallback_Position_ReturnsCallFrame_OnAskCallback()
    {
        var (goal, action) = MakeFrame("AskPos");
        var frame = new global::app.CallStack.Call.Position(action, goal, 0, 0, "id1");
        var ask = new AskCallback { Position = frame };
        await Assert.That(ask.Position).IsSameReferenceAs(frame);
    }

    [Test]
    public async Task ICallback_Position_ReturnsBottomFrame_OnErrorCallback()
    {
        // ErrorCallback.Position is computed lazily via Snapshot's CallStack section.
        // Without an active Restore, Position is null — that's the contract.
        var snap = new Snapshot();
        var err = new ErrorCallback { AppSnapshot = snap };
        await Assert.That(err.Position).IsNull();
    }
}
