using app.module.llm;
using app.module.llm.code;

namespace PLang.Tests.App.Modules.llm;

// An operation one owner composes from another (llm sends its request through http, consent asks through
// output.ask) runs as an action, so a binding on the action's start sees it — the mock a program puts there.
public class ComposedActionsAreObservableTests
{
    private static void Watch(global::app.@this app, string module, string action, List<string> seen)
        => app.actor.list.System.Context.on.Bind(app.type.list["action"], "start", global::app.@event.When.before, (item, _, ctx) =>
        {
            if (item is global::app.goal.step.action.@this started && started.Module.Name == module && started.Name == action)
            {
                lock (seen) seen.Add($"{module}.{action}");
                return Task.FromResult(ctx.Error(new global::app.error.@this("stopped by the watcher", "Watched", 400)));
            }
            return Task.FromResult(ctx.Ok());
        }, global::app.@event.binding.Scope.app);

    // llm's call to the model goes through http.request as an action: a before-binding there sees it.
    [Test] public async Task LlmsHttpCall_IsSeenByABindingOnHttpRequest()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "composed-" + Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
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

    // The consent door asks the actor's own input — its channel's on.ask fires there — never as an output.ask action:
    // a binding on output.ask (which could answer in the actor's place) never sees it.
    [Test] public async Task TheConsentDoorsAsk_GoesToTheActorsInput_NotThroughOutputAsk()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        var seen = new List<string>();
        Watch(app, "output", "ask", seen);
        var input = new Asked(ctx);
        app.actor.list.User.Channel.Register(input);
        var request = global::app.type.item.permission.@this.Request(ctx.Actor!, "/elsewhere", global::app.type.item.permission.Verb.write);

        _ = await ctx.Actor.Permission.Ask("Allow? (y/n/a)", request, ctx, _ => Task.FromResult(ctx.Ok()));

        await Assert.That(input.Count).IsEqualTo(1);
        await Assert.That(seen).DoesNotContain("output.ask");
    }

    // An input that answers no, counting the asks.
    private sealed class Asked : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public int Count { get; private set; }
        public Asked(global::app.actor.context.@this ctx) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Count++;
            return Task.FromResult(action.Context.Ok("n"));
        }
    }
}
