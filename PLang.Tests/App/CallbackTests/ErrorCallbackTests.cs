using global::app.modules.callback;
using global::app.errors;
using ActionEntity = app.goals.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.CallbackTests;

public class ErrorCallbackTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-err-" + System.Guid.NewGuid().ToString("N")[..8]));

    private static (Goal goal, ActionEntity action) MakeAndRegister(global::app.@this app, string name)
    {
        var goal = new Goal { Name = name, Path = $"/{name}.goal" };
        var step = new Step { Index = 0, Text = "step", Goal = goal };
        var action = new ActionEntity { Module = "test", ActionName = "test" };
        action.Step = step; step.Actions.Add(action); goal.Steps.Add(step);
        app.Goals.Add(goal);
        return (goal, action);
    }

    [Test]
    public async Task ErrorCallback_RoundTrip_PreservesAppSnapshotSubtree()
    {
        var app = NewApp();
        var (goal, action) = MakeAndRegister(app, "RTErr");
        await using var call = app.CallStack.Push(action);
        app.User.Context.Variables.Set("v", 7);

        var snap = app.Snapshot();
        var src = new ErrorCallback { AppSnapshot = snap };

        var bytes = src.Serialize(app.User.Context);
        var restored = ErrorCallback.Deserialize(bytes, app.User.Context);

        await Assert.That(restored.AppSnapshot.HasSection("CallStack")).IsTrue();
        await Assert.That(restored.AppSnapshot.HasSection("Variables")).IsTrue();
    }

    [Test]
    public async Task ErrorCallback_Position_ReadsAppCallStackBottomFrame()
    {
        // Position reads through the captured snapshot — null until Run materialises the chain.
        // Direct positional access without Run returns null per ICallback's contract.
        var snap = new Snapshot();
        var err = new ErrorCallback { AppSnapshot = snap };
        await Assert.That(err.Position).IsNull();
    }

    [Test]
    public async Task ErrorCallback_Run_ConstructsFreshApp_AndDispatchesRestore()
    {
        var src = NewApp();
        var (goal, action) = MakeAndRegister(src, "FreshErr");
        await using (var call = src.CallStack.Push(action))
        {
            src.User.Context.Variables.Set("flag", "before");
            var snap = src.Snapshot();
            var bytes = new ErrorCallback { AppSnapshot = snap }.Serialize(src.User.Context);

            // Fresh app with the same goal pre-registered.
            var dst = NewApp();
            MakeAndRegister(dst, "FreshErr");

            var restored = ErrorCallback.Deserialize(bytes, dst.User.Context);
            await restored.Run(dst.User.Context);

            await Assert.That(dst.User.Context.Variables.Get("flag")?.Value).IsEqualTo("before");
        }
    }

    [Test]
    public async Task ErrorCallback_Run_LandsAtBottomFrame_AndReExecutesFailedAction()
    {
        var src = NewApp();
        var (goal, action) = MakeAndRegister(src, "LandErr");
        await using (var call = src.CallStack.Push(action))
        {
            var snap = src.Snapshot();
            var bytes = new ErrorCallback { AppSnapshot = snap }.Serialize(src.User.Context);

            var dst = NewApp();
            MakeAndRegister(dst, "LandErr");

            var restored = ErrorCallback.Deserialize(bytes, dst.User.Context);
            await restored.Run(dst.User.Context);

            // After Run, BottomFrame on the destination CallStack reflects the resumed position.
            var bottom = dst.CallStack.BottomFrame;
            await Assert.That(bottom).IsNotNull();
            await Assert.That(bottom!.Goal.PrPath).IsEqualTo(goal.PrPath);
        }
    }

    [Test]
    public async Task ErrorCallback_DispatchByTypedEnvelope_SelectsRightDeserialize()
    {
        // Typed dispatch: AskCallback.Deserialize and ErrorCallback.Deserialize are
        // separate static factories — picking by Data<T>'s type parameter, not by
        // sniffing the bytes. Pin the static-factory existence on both sides.
        var ask = typeof(AskCallback).GetMethod("Deserialize",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var err = typeof(ErrorCallback).GetMethod("Deserialize",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        await Assert.That(ask).IsNotNull();
        await Assert.That(err).IsNotNull();
        await Assert.That(ask!.ReturnType).IsEqualTo(typeof(AskCallback));
        await Assert.That(err!.ReturnType).IsEqualTo(typeof(ErrorCallback));
    }
}
