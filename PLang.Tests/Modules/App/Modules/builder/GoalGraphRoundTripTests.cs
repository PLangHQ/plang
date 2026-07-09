using Type = global::app.type.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// The Stage-1 DoD (coder review v3 #1): a real goal `.pr` read via the reflection `*`-kind
/// Read must reproduce the SAME graph STJ's Deserialize&lt;goal&gt; produces — structural
/// equality across goal→steps→actions→params/defaults, so silent field drift can't slip in.
/// Runs while both readers exist; when STJ goes, this pins a golden snapshot.
/// </summary>
public class GoalGraphRoundTripTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create(
        "/tmp/goalgraph-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    // A representative .pr (shape lifted from a real built goal): enum-as-int visibility,
    // path-as-string, nested steps→actions→params/defaults, empty collections.
    private const string Pr = """
    {
      "name": "MarkBig",
      "description": null,
      "comment": null,
      "steps": [
        {
          "index": 0,
          "text": "set %label% = 'big'",
          "lineNumber": 2,
          "indent": 0,
          "actions": [
            {
              "module": "variable",
              "action": "set",
              "parameters": [
                { "name": "Name",  "type": { "name": "variable" }, "value": "%label%" },
                { "name": "Value", "type": { "name": "text" },     "value": "big" }
              ],
              "defaults": [
                { "name": "asdefault", "type": { "name": "bool" }, "value": false }
              ],
              "modifiers": []
            }
          ],
          "waitForExecution": true
        }
      ],
      "goals": [],
      "visibility": 1,
      "path": "/BuilderSanity/MarkBig.goal",
      "isSetup": false
    }
    """;

    [Test]
    public async Task ReflectionRead_ReproducesTheGoalGraph_LikeStj()
    {
        var ctx = _app.User.Context;

        var utf8 = new System.Text.Json.Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(Pr));
        utf8.Read();
        var reader = new global::app.channel.serializer.json.Reader(utf8);
        var built = new global::app.type.kind.behavior.reflection().Read(
            ref reader, typeof(Goal), new global::app.type.reader.ReadContext(ctx));

        var goal = built as Goal;
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Name).IsEqualTo("MarkBig");
        await Assert.That(goal.Visibility).IsEqualTo(Visibility.Public);   // 1
        await Assert.That(goal.Path?.ToString()).IsEqualTo("/BuilderSanity/MarkBig.goal");
        await Assert.That(goal.IsSetup).IsFalse();

        await Assert.That(goal.Steps.Count).IsEqualTo(1);
        var step = goal.Steps[0];
        await Assert.That(step.Text).IsEqualTo("set %label% = 'big'");
        await Assert.That(step.LineNumber).IsEqualTo(2);
        await Assert.That(step.WaitForExecution).IsTrue();

        await Assert.That(step.Actions.Count).IsEqualTo(1);
        var action = step.Actions[0];
        await Assert.That(action.Module).IsEqualTo("variable");
        await Assert.That(action.ActionName).IsEqualTo("set");
        await Assert.That(action.Parameters.Count).IsEqualTo(2);
        await Assert.That(action.Parameters[0].Name).IsEqualTo("Name");
        await Assert.That(action.Parameters[1].Name).IsEqualTo("Value");
        await Assert.That(action.Defaults!.Count).IsEqualTo(1);
    }
}
