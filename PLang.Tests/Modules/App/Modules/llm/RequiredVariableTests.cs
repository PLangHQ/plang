namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// A required parameter given a variable that holds nothing is refused before the action runs, naming the variable —
/// never a NullReferenceException inside it. The shape a builder wrote for a step too vague to say anything
/// ("make it better"): a write, then <c>llm.query(Message=%!data%)</c>, where the write left %!data% nothing.
/// </summary>
public class RequiredVariableTests
{
    // A message leaving for the model is used: its variables are what they hold, never their names.
    [Test]
    public async Task AMessagesVariables_AreSentAsWhatTheyHold()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var service = LlmTestHelper.SetupMockHttp(app);
        service.Handler = _ => Task.FromResult(LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("hello")));
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("name", ctx.Text("Ada"));
        var goal = await RealGoalLoad.Read(app, """
            {"name": "Start", "path": "/Start.goal", "step": [
              {"index": 0, "text": "ask the model \"Say hello to %name%\"", "line": {"number": 2}, "code": [
                {"module": "llm", "name": "query", "property": [
                  {"name": "Message", "type": {"name": "list", "kind": "message", "template": "plang"},
                   "value": [{"Role": "user", "Content": "Say hello to %name%"}],
                   "variable": [{"text": "%name%", "code": [{"variable": "name"}]}]}]}]}]}
            """);

        await (await goal.Step[0].Start(ctx)).IsSuccess();

        var body = await service.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(body).Contains("Say hello to Ada");
        await Assert.That(body).DoesNotContain("%name%");
        // the program itself is kept as written: its .pr row still names the variable
        await Assert.That(await ctx.Pr(goal)).Contains("Say hello to %name%");
    }

    // a message whose variable isn't there fails the read, whole — never sent with an empty hole
    [Test]
    public async Task AMessagesVariable_NotSet_FailsTheRead_AndNothingIsSent()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var service = LlmTestHelper.SetupMockHttp(app);
        var ctx = app.actor.list.User.Context;
        var goal = await RealGoalLoad.Read(app, """
            {"name": "Start", "path": "/Start.goal", "step": [
              {"index": 0, "text": "ask the model \"Say hello to %nobody%\"", "line": {"number": 2}, "code": [
                {"module": "llm", "name": "query", "property": [
                  {"name": "Message", "type": {"name": "list", "kind": "message", "template": "plang"},
                   "value": [{"Role": "user", "Content": "Say hello to %nobody%"}],
                   "variable": [{"text": "%nobody%", "code": [{"variable": "nobody"}]}]}]}]}]}
            """);

        var asked = await goal.Step[0].Start(ctx);

        await asked.IsFailure();
        await Assert.That(service.CallCount).IsEqualTo(0);
    }

    // a value whose method does something each time it is called (here, counts)
    private sealed class Counter(global::app.type.@this type) : global::app.type.item.@this(type)
    {
        internal int Calls;
        [global::app.LlmBuilder] public global::app.type.item.text.@this Next() { Calls++; return "name"; }
    }

    // the guard asks only a variable whose read only looks up: one that calls a method is left to the action, so the
    // method runs once
    [Test]
    public async Task ARequiredVariableThatCallsAMethod_RunsItOnce()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        var counter = new Counter(app.type.list["item"]);
        await ctx.Variable.Set("counter", counter);
        await ctx.Variable.Set("rows", global::app.type.item.@this.Create(new List<object?> { new Dictionary<string, object?> { ["name"] = "x" } }, ctx));

        await Make.Action(ctx, "list", "any", Make.Param(ctx, "ListName", "rows", "variable"), ("Key", "%counter.Next()%"),
            ("Operator", "=="), ("Value", "x")).Start(ctx);

        await Assert.That(counter.Calls).IsEqualTo(1);
    }

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
