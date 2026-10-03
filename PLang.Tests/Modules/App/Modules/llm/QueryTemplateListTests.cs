using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Modules.llm;

// A message written as a bare text in the query's message list (`Message: ["Sort these: %items%"]`) is the user's
// message, its variables read where the step is: what reaches the model is the rendered text, never the template.
public class QueryTemplateListTests
{
    [Test]
    public async Task ATextInTheMessageList_ReachesTheModelRendered_AsTheUsersMessage()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "llm_template_list_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(dir);
        await using var app = new global::app.@this(dir).Testing();
        var handler = LlmTestHelper.SetupMockHttp(app);
        string? sent = null;
        handler.Handler = async request =>
        {
            sent = await request.Content!.ReadAsStringAsync();
            return LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("ok"));
        };
        var ctx = app.actor.list.User.Context;
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(ctx, "D6", "/D6.goal",
            Make.Step("set %items% = [\"b\", \"a\"]", Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "items", "variable"),
                ("Value", new List<object?> { "b", "a" }))),
            Make.Step("ask llm \"Sort these: %items%\"", Make.Action(ctx, "llm", "query", ("Message", new List<object?> { "Sort these: %items%" })))));
        app.goal.list.Add(goal);

        await (await goal.Start(ctx)).IsSuccess();

        var message = System.Text.Json.JsonDocument.Parse(sent!).RootElement.GetProperty("messages")[0];
        await Assert.That(message.GetProperty("role").GetString()).IsEqualTo("user");
        await Assert.That(message.GetProperty("content").GetString()).IsEqualTo("Sort these: [\"b\",\"a\"]");
    }
}
