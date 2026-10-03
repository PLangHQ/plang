using app.module.llm;
using Dict = global::app.type.item.dict.@this;

namespace PLang.Tests.App.Modules.llm;

// The decider's key and endpoint are its own settings — %!llm.decider.setting.key% / .endpoint, a class the action
// names: set for this run or saved on the actor's row, read by TypeSafe, never shown.
public class DeciderSettingTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app;
    private readonly MockHttpMessageHandler _handler;

    public DeciderSettingTests()
    {
        _app = new global::app.@this("/tmp/decider-setting-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        _handler = LlmTestHelper.SetupMockHttp(_app);
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;

    private static global::app.type.item.variable.@this Variable(string path)
        => new global::app.type.item.variable.parser.@this(path).Variable.Single();

    private async Task Set(string path, string value)
        => await (await Variable(path).Set(Ctx.Ok((global::app.type.item.text.@this)value), Ctx)).IsSuccess();

    [Test] public async Task TheKeyAndEndpoint_AreTheDecidersOwnSettings()
    {
        await Set("%!llm.decider.setting.endpoint%", "https://decide.example/v1");

        var endpoint = await Variable("%!llm.decider.setting.endpoint%").Start(Ctx);

        await Assert.That((await endpoint.Value())?.ToString()).IsEqualTo("https://decide.example/v1");
        await Assert.That(Ctx.Setting.Of<decider.setting>().Endpoint.ToString()).IsEqualTo("https://decide.example/v1");
    }

    [Test] public async Task TypeSafe_AsksWhereTheSettingSays_WithItsKey()
    {
        await Set("%!llm.decider.setting.endpoint%", "https://decide.example/v1");
        await Set("%!llm.decider.setting.key%", "decider-key-1");
        _handler.Handler = _ => Task.FromResult(LlmTestHelper.JsonResponse("""{"answers": {"s0": {"noul": 0.9}}}"""));
        var action = new decider(Ctx)
        {
            State = new global::app.data.@this<global::app.type.item.text.@this>("", "A plang goal."),
            Question = new("", (Dict)global::app.type.item.@this.Create(new Dictionary<string, object?>
            {
                ["s0"] = new Dictionary<string, object?> { ["type"] = "noul", ["instructions"] = "?" },
            }, Ctx), context: Ctx),
        };
        await action.Attach(null, Ctx);

        await (await action.Start()).IsSuccess();

        await Assert.That(_handler.LastRequest!.RequestUri!.ToString()).IsEqualTo("https://decide.example/v1");
        await Assert.That(_handler.LastRequest.Headers.Authorization?.ToString()).IsEqualTo("Bearer decider-key-1");
    }

    [Test] public async Task ASavedKey_IsReadBack()
    {
        var setting = Ctx.Setting.Of<decider.setting>().Copy();
        ((decider.setting)setting).Key = "saved-key-2";

        await (await Ctx.Setting.Save(setting)).IsSuccess();

        await Assert.That(Ctx.Setting.Of<decider.setting>().Key.ToString()).IsEqualTo("saved-key-2");
    }

    [Test] public async Task TheKey_IsNeverShown()
    {
        await Set("%!llm.decider.setting.key%", "shown-never-3");

        var setting = await Variable("%!llm.decider.setting%").Start(Ctx);
        var written = (await Ctx.Format("application/json").Serialize(setting, Ctx).Value())!.ToString();

        await Assert.That(written).DoesNotContain("shown-never-3");
    }
}
