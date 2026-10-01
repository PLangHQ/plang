using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Render = global::app.module.ui.Render;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>
/// A value in a template reads as itself: a span as its own text (never Fluid's date), and <c>| text</c> writes a
/// value as plang's text format does — a leaf bare, a container as its json.
/// </summary>
public class FluidTextTests
{
    private static async Task<string> Render(global::app.@this app, string template, object value)
    {
        var ctx = app.actor.list.System.Context;
        await ctx.Variable.Set(new global::app.data.@this("x", value, context: ctx));
        var action = new Render(ctx)
        {
            Template = (global::app.type.item.text.@this)template,
            IsFile = (global::app.type.item.@bool.@this)false,
        };
        var result = await new global::app.module.ui.code.Fluid().Render(action);
        await result.IsSuccess();
        return (await result.Value())?.ToString() ?? "";
    }

    [Test] public async Task Duration_RendersAsItsText_NotADate()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.System.Context;
        var holder = global::app.type.item.@this.Create(new Dictionary<string, object?>
            { ["timeout"] = new global::app.type.item.duration.@this(System.TimeSpan.FromSeconds(30)) }, ctx);
        await Assert.That(await Render(app, "{{ x.timeout }}", holder)).IsEqualTo("30s");
        await Assert.That(await Render(app, "{{ x.timeout | text }}", holder)).IsEqualTo("30s");
    }

    [Test] public async Task TextFilter_WritesAContainerAsItsJson()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.System.Context;
        var limit = app.Module("llm")["query"]!.Property["limit"]!.Default!;
        var holder = global::app.type.item.@this.Create(new Dictionary<string, object?> { ["limit"] = limit }, ctx);
        await Assert.That(await Render(app, "{{ x.limit | text }}", holder)).IsEqualTo("{\"token\":16000,\"tool\":10,\"retry\":0}");
    }
}
