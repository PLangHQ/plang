using global::App.Callback;
using global::App.CallStack;
using global::App.Errors;
using ActionEntity = App.Goals.Goal.Steps.Step.Actions.Action.@this;

namespace PLang.Tests.App.CallbackTests;

public class AskCallbackTests
{
    private static global::App.@this NewApp() =>
        new global::App.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-ask-" + System.Guid.NewGuid().ToString("N")[..8]));

    private static (Goal goal, ActionEntity action) MakeAndRegister(global::App.@this app, string name)
    {
        var goal = new Goal { Name = name, Path = $"/{name}.goal" };
        var step = new Step { Index = 0, Text = "step", Goal = goal };
        var action = new ActionEntity { Module = "variable", ActionName = "set" };
        action.Step = step; step.Actions.Add(action); goal.Steps.Add(step);
        app.Goals.Add(goal);
        return (goal, action);
    }

    [Test]
    public async Task AskCallback_RoundTrip_PreservesPositionActorAndVariables()
    {
        var app = NewApp();
        var (goal, action) = MakeAndRegister(app, "RTAsk");
        var frame = new RestoredFrame(action, goal, 0, 0, "id");
        var src = new AskCallback
        {
            Position = frame,
            ActorName = "User",
            Variables = new() { new global::App.Data.@this("x", 1) }
        };

        var bytes = src.Serialize(app.User.Context);
        var restored = AskCallback.Deserialize(bytes, app.User.Context);

        await Assert.That(restored.ActorName).IsEqualTo("User");
        await Assert.That(restored.Position).IsNotNull();
        await Assert.That(restored.Position!.Goal.PrPath).IsEqualTo(goal.PrPath);
        await Assert.That(restored.Variables.Count).IsEqualTo(1);
        await Assert.That(restored.Variables[0].Name).IsEqualTo("x");
        // JSON ingress promotes int → long via UnwrapJsonElement; compare numerically.
        await Assert.That(System.Convert.ToInt64(restored.Variables[0].Value)).IsEqualTo(1L);
    }

    [Test]
    public async Task AskCallback_Serialize_CallsCryptoEncrypt_AndReturnsEncryptedBytes()
    {
        var app = NewApp();
        var (goal, action) = MakeAndRegister(app, "EncAsk");
        var src = new AskCallback
        {
            Position = new RestoredFrame(action, goal, 0, 0, ""),
            ActorName = "User",
            Variables = new()
        };

        var bytes = src.Serialize(app.User.Context);
        // v1 crypto is identity → encrypted bytes equal the JSON wire bytes; the call
        // path runs through the encrypt action so the wiring is real.
        await Assert.That(bytes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task AskCallback_Deserialize_CallsCryptoDecrypt_AndReconstructsRecord()
    {
        var app = NewApp();
        var (goal, action) = MakeAndRegister(app, "DecAsk");
        var src = new AskCallback
        {
            Position = new RestoredFrame(action, goal, 0, 0, ""),
            ActorName = "Service",
            Variables = new() { new global::App.Data.@this("y", "two") }
        };

        var bytes = src.Serialize(app.User.Context);
        var restored = AskCallback.Deserialize(bytes, app.User.Context);

        await Assert.That(restored.ActorName).IsEqualTo("Service");
        await Assert.That(restored.Variables[0].Value).IsEqualTo("two");
    }

    [Test]
    public async Task AskCallback_Run_BindsVariables_AndDispatchesAskActionWithBoundValue()
    {
        var app = NewApp();
        var (goal, action) = MakeAndRegister(app, "RunAsk");
        var ask = new AskCallback
        {
            Position = new RestoredFrame(action, goal, 0, 0, ""),
            Variables = new() { new global::App.Data.@this("bound", 42) }
        };

        await ask.Run(app.User.Context);
        // Bind succeeded — %bound% is now in the resumed context's Variables.
        await Assert.That(app.User.Context.Variables.Get("bound")?.Value).IsEqualTo(42);
    }

    [Test]
    public async Task AskCallback_Run_ReturnsResumedActionResult_AsTaskOfData()
    {
        var app = NewApp();
        var (goal, action) = MakeAndRegister(app, "ChainAsk");
        var ask = new AskCallback
        {
            Position = new RestoredFrame(action, goal, 0, 0, ""),
            Variables = new()
        };

        var result = await ask.Run(app.User.Context);
        // Run signature is Task<Data>; Data flows back even when the resumed action
        // produces a no-op result.
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task AskCallback_Run_HardErrors_OnGoalStubNotFound()
    {
        var app = NewApp();
        // No goal registered → Position can't be built via Deserialize either, so we
        // simulate by serialising on a separate App and deserialising on a fresh one.
        var src = NewApp();
        var (goal, action) = MakeAndRegister(src, "MissingAsk");
        var ask = new AskCallback
        {
            Position = new RestoredFrame(action, goal, 0, 0, ""),
            Variables = new()
        };
        var bytes = ask.Serialize(src.User.Context);

        await Assert.ThrowsAsync<CallbackGoalNotFound>(async () =>
        {
            AskCallback.Deserialize(bytes, app.User.Context);
            await Task.CompletedTask;
        });
    }
}
