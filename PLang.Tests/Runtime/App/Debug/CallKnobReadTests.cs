namespace PLang.Tests.App.Debug;

// What the call stack captures reads in plang as it is set: %!app.call.setting.timing% and the stack's own knob.
public class CallKnobReadTests
{
    private static async Task<string?> Read(string path, global::app.actor.context.@this context)
        => (await (await new global::app.type.item.variable.parser.@this(path, context.App.type.list).Variable.Single().Start(context)).Value())?.ToString();

    [Test]
    public async Task TheCallSettingsTiming_ReadsAsSet()
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;
        await Assert.That(await Read("%!app.call.setting.timing%", context)).IsEqualTo("false");

        context.Setting.Set("app.call.setting.timing", true);

        await Assert.That(await Read("%!app.call.setting.timing%", context)).IsEqualTo("true");
    }
}
