using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.DataTests;

public class FlipProbeTests
{
    [Test] public async Task TextPlainNarrowsToText()
    {
        await using var app = new global::app.@this("/test");
        var ctx = new global::app.actor.context.@this(app);
        var t = app.Format.TypeFromMime("text/plain");
        await Assert.That(t.Name + "/" + t.Kind).IsEqualTo("binary/txt");
        var d = global::app.data.@this.FromRaw(System.Text.Encoding.UTF8.GetBytes("Hello World"), t, ctx, "r");
        var v = await d.Value();
        await Assert.That(v?.ToString()).IsEqualTo("Hello World");
    }
    [Test] public async Task TextHtmlNarrowsToText()
    {
        await using var app = new global::app.@this("/test");
        var ctx = new global::app.actor.context.@this(app);
        var t = app.Format.TypeFromMime("text/html");
        var d = global::app.data.@this.FromRaw(System.Text.Encoding.UTF8.GetBytes("<p>hi</p>"), t, ctx, "r");
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("<p>hi</p>");
    }
}
