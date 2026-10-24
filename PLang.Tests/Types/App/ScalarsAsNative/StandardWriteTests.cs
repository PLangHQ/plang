namespace PLang.Tests.App.ScalarsAsNative;

// A duration or size born with no standard (a step's time, a file's length) is written in the one its asker's setting
// names, read when it is written; one asked for in a standard (`as iso`, `as si`) writes itself in it, whatever the
// setting says.
public class StandardWriteTests
{
    private static async Task<string> Written(global::app.type.item.@this value, global::app.actor.context.@this context)
    {
        using var text = new System.IO.MemoryStream();
        await (await context.App.type.list.Mime("text/plain").Encode(text, new global::app.data.@this("v", value, context: context), context)).IsSuccess();
        return System.Text.Encoding.UTF8.GetString(text.ToArray());
    }

    [Test]
    public async Task AnUnnamedDuration_IsWrittenInTheAskersStandard()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var span = global::app.type.item.duration.@this.Create(System.TimeSpan.FromSeconds(90), context);

        await Assert.That(await Written(span, context)).IsEqualTo("1m30s");
        context.Setting.Set("app.type.duration.setting.standard", "iso");
        await Assert.That(await Written(span, context)).IsEqualTo("PT1M30S");
    }

    [Test]
    public async Task ANamedDuration_IgnoresTheSetting()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        context.Setting.Set("app.type.duration.setting.standard", "short");
        var iso = global::app.type.item.duration.@this.Create(System.TimeSpan.FromSeconds(90),
            new global::app.type.@this("duration", "iso"), new global::app.data.@this("d", context: context))!;

        await Assert.That(await Written(iso, context)).IsEqualTo("PT1M30S");
    }

    [Test]
    public async Task AnUnnamedSize_IsWrittenInTheAskersStandard()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var size = global::app.type.item.size.@this.Create(100_000_000L, context);

        await Assert.That(await Written(size, context)).IsEqualTo("95.4 MiB");
        context.Setting.Set("app.type.size.setting.standard", "si");
        await Assert.That(await Written(size, context)).IsEqualTo("100 MB");
    }

    [Test]
    public async Task ANamedSize_IgnoresTheSetting()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        context.Setting.Set("app.type.size.setting.standard", "iec");
        var si = global::app.type.item.size.@this.Create(100_000_000L,
            new global::app.type.@this("size", "si"), new global::app.data.@this("s", context: context))!;

        await Assert.That(await Written(si, context)).IsEqualTo("100 MB");
    }
}
