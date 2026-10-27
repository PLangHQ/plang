using app.module.llm;
using Dict = global::app.type.item.dict.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>Security probe (decider-key): a key the user pastes is saved on the asking actor's row only, is shown nowhere
/// (errors, Debug dump of the setting), and goes to the setting's endpoint only. Asserts the SAFE behaviour.</summary>
public class DeciderKeyProbeTests
{
    private const string Pasted = "tsk-probe-9f3a77";

    private static async Task<(global::app.@this app, MockHttpMessageHandler service, System.IO.MemoryStream written)> Setup(string status)
    {
        var app = new global::app.@this("/tmp/dkp-" + System.Guid.NewGuid().ToString("N")[..8], global::PLang.Tests.Parent.Actor).Testing();
        var service = LlmTestHelper.SetupMockHttp(app);
        service.Handler = status == "ok"
            ? _ => Task.FromResult(LlmTestHelper.JsonResponse("""{"answers": {"s0": {"noul": 0.9}}, "usage": {"questions": 1}}"""))
            : _ => Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized) { Content = new System.Net.Http.StringContent("bad key") });
        var written = new System.IO.MemoryStream();
        var user = app.actor.list.User;
        user.Channel.Register(new global::app.channel.type.stream.@this(user.Channel, global::app.channel.list.@this.Output, written,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain", Framed = true });
        user.Channel.Register(new global::app.channel.type.stream.@this(user.Channel, global::app.channel.list.@this.Input,
            new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(Pasted + "\n")), global::app.channel.ChannelDirection.Input, ownsStream: true));
        await Task.CompletedTask;
        return (app, service, written);
    }

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

    [Test]
    public async Task APastedKey_IsSavedOnTheAskersRowOnly_AndShownNowhere()
    {
        var (app, service, written) = await Setup("ok");
        await using var _ = app;
        var user = app.actor.list.User.Context;

        await (await Decided(user)).IsSuccess();

        // another actor's settings do not hold it
        var system = await app.actor.list.System.Context.Setting.Get<global::app.module.llm.decider.setting>();
        await Assert.That(system.Key.ToString()).DoesNotContain(Pasted);
        // the setting's debug dump masks it; nothing written to the user's output holds it
        using var debug = new System.IO.MemoryStream();
        var setting = await user.Setting.Get<global::app.module.llm.decider.setting>();
        await (await app.type.list.Plang.Encode(debug, new global::app.data.@this("s", setting, context: user), user, global::app.View.Debug)).IsSuccess();
        await Assert.That(System.Text.Encoding.UTF8.GetString(debug.ToArray())).DoesNotContain(Pasted);
        await Assert.That(System.Text.Encoding.UTF8.GetString(written.ToArray())).DoesNotContain(Pasted);
        // sent once, to the setting's endpoint
        await Assert.That(service.LastRequest!.RequestUri!.ToString()).IsEqualTo(setting.Endpoint.ToString());
    }

    [Test]
    public async Task AServiceRefusal_DoesNotEchoTheKey()
    {
        var (app, _, _) = await Setup("401");
        await using var _app = app;
        var decided = await Decided(app.actor.list.User.Context);

        await decided.IsFailure();
        var all = decided.Error!.Message + string.Join("|", decided.Error.list.Select(e => e.Message)) + decided.Error.Exception?.ToString();
        await Assert.That(all).DoesNotContain(Pasted);
    }
}
