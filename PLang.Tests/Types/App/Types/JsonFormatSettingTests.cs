namespace PLang.Tests.App.Types;

// json's layout is the program's say: %!app.type.kind.json.setting.format% — formatted (the default) or compact. Only
// json's own encode reads it: plang's wire, a digest and a .pr have writers of their own.
public class JsonFormatSettingTests
{
    private const string Address = "app.type.kind.json.setting.format";

    private static global::app.@this App() =>
        new global::app.@this("/tmp/jsonfmt-" + System.Guid.NewGuid().ToString("N")[..8], global::PLang.Tests.Parent.Actor).Testing();

    private static global::app.data.@this Dict(global::app.@this app) =>
        new("d", new global::app.type.item.dict.@this(app.type.list[typeof(global::app.type.item.dict.@this)])
            .Set("name", (global::app.type.item.text.@this)"þú <b> & '—'").Set("n", global::app.type.item.@this.Create(1L, app.actor.list.User.Context)),
            context: app.actor.list.User.Context);

    private static async Task<string> Written(global::app.@this app, string mime, global::app.data.@this data)
    {
        using var stream = new System.IO.MemoryStream();
        await (await app.type.list.Mime(mime).Encode(stream, data, app.actor.list.User.Context)).IsSuccess();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Compact(global::app.@this app) =>
        app.actor.list.User.Context.Setting.Set(Address, new global::app.type.item.text.@this("compact",
            app.type.list[typeof(global::app.type.item.text.@this)]));

    [Test]
    public async Task Json_IsFormatted_ByDefault()
    {
        await using var app = App();

        await Assert.That(await Written(app, "application/json", Dict(app)))
            .IsEqualTo("{\n  \"name\": \"þú <b> & '—'\",\n  \"n\": 1\n}");
    }

    [Test]
    public async Task Json_IsCompact_WhenTheSettingSays()
    {
        await using var app = App();
        Compact(app);

        await Assert.That(await Written(app, "application/json", Dict(app))).IsEqualTo("{\"name\":\"þú <b> & '—'\",\"n\":1}");
    }

    [Test]
    public async Task TheWire_IsUntouched()
    {
        await using var app = App();
        var formatted = await Written(app, "application/plang", Dict(app));
        Compact(app);

        await Assert.That(await Written(app, "application/plang", Dict(app))).IsEqualTo(formatted);
        await Assert.That(formatted).DoesNotContain("\n");
    }

    [Test]
    public async Task ADigest_IsUntouched()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        async Task<string> Poured()
        {
            using var stream = new System.IO.MemoryStream();
            await (await (await Dict(app).Value())!.Pour(stream, context)).IsSuccess();
            return System.Convert.ToHexString(stream.ToArray());
        }
        var formatted = await Poured();
        Compact(app);

        await Assert.That(await Poured()).IsEqualTo(formatted);
    }

    [Test]
    public async Task APr_IsUntouched()
    {
        await using var app = App();
        var goal = await RealGoalLoad.Read(app, """
            {"name": "Start", "path": "/Start.goal", "step": [
              {"index": 0, "text": "set x", "line": {"number": 2}, "code": [
                {"module": "variable", "name": "set", "property": [
                  {"name": "Name", "type": {"name": "variable"}, "value": "%x%", "variable": [{"text": "%x%", "code": [{"variable": "x"}]}]},
                  {"name": "Value", "type": {"name": "text"}, "value": "hello"}]}]}]}
            """);
        var context = app.actor.list.User.Context;
        async Task<string> Pr()
        {
            using var stream = new System.IO.MemoryStream();
            await (await app.type.list["goal"].kind.Encode(stream, context.Ok(goal), context, global::app.View.Store)).IsSuccess();
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        var formatted = await Pr();
        Compact(app);

        await Assert.That(await Pr()).IsEqualTo(formatted);
    }
}
