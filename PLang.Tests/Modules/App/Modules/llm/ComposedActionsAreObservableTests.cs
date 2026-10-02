using app.module.llm;
using app.module.llm.code;

namespace PLang.Tests.App.Modules.llm;

// An operation one owner composes from another (llm sends its request through http, consent asks through
// output.ask) runs as an action, so a binding on the action's start sees it — the mock a program puts there.
public class ComposedActionsAreObservableTests
{
    private static void Watch(global::app.@this app, string module, string action, List<string> seen)
        => app.type.list["action"].Own().Bind("start", global::app.@event.When.before, (item, _, ctx) =>
        {
            if (item is global::app.goal.step.action.@this started && started.Module.Name == module && started.Name == action)
            {
                lock (seen) seen.Add($"{module}.{action}");
                return Task.FromResult(ctx.Error(new global::app.error.Error("stopped by the watcher", "Watched", 400)));
            }
            return Task.FromResult(ctx.Ok());
        }, app.actor.list.System, global::app.@event.binding.Scope.app);

    // llm's call to the model goes through http.request as an action: a before-binding there sees it.
    [Test] public async Task LlmsHttpCall_IsSeenByABindingOnHttpRequest()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "composed-" + Guid.NewGuid().ToString("N")[..8])).Testing();
        LlmTestHelper.SetupMockHttp(app);
        var ctx = app.actor.list.System.Context;
        var seen = new List<string>();
        Watch(app, "http", "request", seen);

        var query = new query(ctx)
        {
            Message = new List<LlmMessage> { new LlmMessage { Role = "user", Content = "composed " + Guid.NewGuid() } }.ToListData<LlmMessage>(ctx),
        };
        _ = await new global::app.goal.step.action.@this(query, ctx).Start(ctx);

        await Assert.That(seen).Contains("http.request");
    }

    // The consent door asks through output.ask as an action: on.ask fires for it as for any ask.
    [Test] public async Task TheConsentDoorsAsk_IsSeenByABindingOnOutputAsk()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var seen = new List<string>();
        Watch(app, "output", "ask", seen);
        var request = global::app.type.item.permission.@this.Request(ctx.Actor!.Name, "/elsewhere", global::app.type.item.permission.Verb.write);

        _ = await ctx.Actor.Permission.Ask("Allow? (y/n/a)", request, ctx, _ => Task.FromResult(ctx.Ok()));

        await Assert.That(seen).Contains("output.ask");
    }
}
