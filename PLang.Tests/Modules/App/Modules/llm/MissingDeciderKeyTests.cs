using app.module.llm;
using Dict = global::app.type.item.dict.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// The decider's key asks as the llm's does: one held answers at once; none held, the user is asked once — secretly —
/// and the answer saved on their own settings row; with no one to ask, the build fails naming both ways to give one,
/// and no request reaches the service.
/// </summary>
public class MissingDeciderKeyTests
{
    private const string Pasted = "typesafe-key-4711";

    private static (global::app.@this App, System.IO.MemoryStream Written, MockHttpMessageHandler Service) Asking(string? typed)
    {
        var app = new global::app.@this("/tmp/deciderkey-" + System.Guid.NewGuid().ToString("N")[..8], global::PLang.Tests.Parent.Actor).Testing();
        var service = LlmTestHelper.SetupMockHttp(app);
        service.Handler = _ => Task.FromResult(LlmTestHelper.JsonResponse("""{"answers": {"s0": {"noul": 0.9}}, "usage": {"questions": 1}}"""));
        var written = new System.IO.MemoryStream();
        var user = app.actor.list.User;
        user.Channel.Register(new global::app.channel.type.stream.@this(user.Channel, global::app.channel.list.@this.Output, written,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain", Framed = true });
        if (typed != null)
            user.Channel.Register(new global::app.channel.type.stream.@this(user.Channel, global::app.channel.list.@this.Input,
                new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(typed)), global::app.channel.ChannelDirection.Input, ownsStream: true));
        else
            user.Channel.Remove(global::app.channel.list.@this.Input, user.Context);
        return (app, written, service);
    }

    // one question to the decider, as the builder asks it
    private static async Task<global::app.data.@this> Decided(global::app.actor.context.@this context)
    {
        var action = new decider(context)
        {
            State = new global::app.data.@this<global::app.type.item.text.@this>("", "This is a plang goal called Start.", context: context),
            Question = new global::app.data.@this<Dict>("", (Dict)global::app.type.item.@this.Create(new Dictionary<string, object?>
            {
                ["s0"] = new Dictionary<string, object?> { ["type"] = "noul", ["instructions"] = "Does step 0 use the file module?" },
            }, context), context: context),
        };
        await action.Attach(null, context);
        return await action.Start();
    }

    // the decider's setting with no key held, asked through its key
    private static global::app.module.llm.setting.IKey Unkeyed()
        => new global::app.module.llm.decider.setting(new global::app.type.@this(typeof(global::app.module.llm.decider.setting))) { Key = "" };

    [Test]
    public async Task AMissingKey_IsAskedOnce_Saved_AndNeverShown()
    {
        var (app, written, service) = Asking(Pasted + "\n");
        await using var _ = app;
        var ctx = app.actor.list.User.Context;

        var asked = await Unkeyed().Asked(ctx);
        var decided = await Decided(ctx);

        await asked.IsSuccess();
        await Assert.That((await asked.Value())!.ToString()).IsEqualTo(Pasted);
        await decided.IsSuccess();   // the input is spent: asking again would have failed
        await Assert.That(service.LastRequest!.Headers.Authorization?.Parameter).IsEqualTo(Pasted);
        var shown = System.Text.Encoding.UTF8.GetString(written.ToArray());
        await Assert.That(shown).Contains("TypeSafe key");
        await Assert.That(shown).DoesNotContain(Pasted);
    }

    [Test]
    public async Task NoOneToAsk_FailsNamingBothWays_AndSendsNothing()
    {
        var (app, _, service) = Asking(null);
        await using var _app = app;
        var ctx = app.actor.list.User.Context;
        ctx.Setting.Set("llm.decider.setting.key", "");

        var decided = await Decided(ctx);

        await decided.IsFailure();
        await Assert.That(decided.Error!.Key).IsEqualTo("MissingDeciderKey");
        await Assert.That(decided.Error.Message).Contains("%!llm.decider.setting.key%");
        await Assert.That(decided.Error.Message).Contains("TYPESAFE_API_KEY");
        await Assert.That(service.CallCount).IsEqualTo(0);
    }

    [Test]
    public async Task AHeldKey_IsNotAsked()
    {
        var (app, written, service) = Asking(null);
        await using var _app = app;
        var ctx = app.actor.list.User.Context;
        ctx.Setting.Set("llm.decider.setting.key", "held-key");

        var decided = await Decided(ctx);

        await decided.IsSuccess();
        await Assert.That(service.LastRequest!.Headers.Authorization?.Parameter).IsEqualTo("held-key");
        await Assert.That(System.Text.Encoding.UTF8.GetString(written.ToArray())).DoesNotContain("TypeSafe key");
    }
}
