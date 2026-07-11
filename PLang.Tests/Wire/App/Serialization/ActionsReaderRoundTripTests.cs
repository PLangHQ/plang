namespace PLang.Tests.App.Serialization;

// Verifies the `actions` reader routing: a deferred Data whose declared type is `actions`
// (exactly what the data reader builds for the error.handle recovery chain) materializes
// back to real actions THROUGH the registered reader — not the deleted Convert hook / FromWire.
public class ActionsReaderRoundTripTests
{
    private static global::app.@this NewApp() => global::PLang.Tests.TestApp.Create(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-actions-" + Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task Actions_DeferredSource_MaterializesThroughReader()
    {
        var app = NewApp();
        await using (app)
        {
            var ctx = app.User.Context;
            // The wire shape of a recovery chain value: a bare array of action records.
            var raw = "[{\"module\":\"goal\",\"action\":\"call\"}]";
            var type = new global::app.type.@this("actions");
            // Exactly what the data reader builds for a deferred `actions` value slot.
            var data = new global::app.data.@this("", type.Create(raw, ctx, "application/plang"), context: ctx);

            var actions = ((await data.Value()) as global::app.type.clr.@this<global::app.goal.steps.step.actions.@this>)?.Value;
            await Assert.That(actions).IsNotNull();
            await Assert.That(actions!.Count).IsEqualTo(1);
            await Assert.That(actions[0].Module).IsEqualTo("goal");
            await Assert.That(actions[0].ActionName).IsEqualTo("call");
        }
    }
}
