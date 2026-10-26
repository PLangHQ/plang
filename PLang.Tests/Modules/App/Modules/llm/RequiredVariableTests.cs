namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// A required parameter given a variable that holds nothing is refused before the action runs, naming the variable —
/// never a NullReferenceException inside it. The shape a builder wrote for a step too vague to say anything
/// ("make it better"): a write, then <c>llm.query(Message=%!data%)</c>, where the write left %!data% nothing.
/// </summary>
public class RequiredVariableTests
{
    [Test]
    public async Task ARequiredVariableHoldingNothing_IsRefused_NamingIt_AndNothingIsSent()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var service = LlmTestHelper.SetupMockHttp(app);
        var ctx = app.actor.list.User.Context;
        var goal = await RealGoalLoad.Read(app, """
            {"name": "Start", "path": "/Start.goal", "step": [
              {"index": 0, "text": "write out Ada", "line": {"number": 2}, "code": [
                {"module": "output", "name": "write", "property": [{"name": "Data", "type": {"name": "text"}, "value": "Ada"}]}]},
              {"index": 1, "text": "make it better", "line": {"number": 3}, "code": [
                {"module": "llm", "name": "query", "property": [
                  {"name": "Message", "type": {"name": "list", "kind": "message", "template": "plang"}, "value": "%!data%",
                   "variable": [{"text": "%!data%", "code": [{"variable": "!data"}]}]}]}]}]}
            """);

        await (await goal.Step[0].Start(ctx)).IsSuccess();
        var asked = await goal.Step[1].Start(ctx);

        await asked.IsFailure();
        await Assert.That(asked.Error!.Key).IsEqualTo("ValueRequired");
        await Assert.That(asked.Error.Message).Contains("%!data%");
        await Assert.That(service.CallCount).IsEqualTo(0);
    }
}
