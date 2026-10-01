using System.Text.Json.Nodes;
using app.Attributes;

namespace app.module.screen;

/// <summary>
/// One of a screen's elements, by its selector — <c>%!screen.element["#window.bot"]%</c> (a window's ☰; a window's
/// own parts are named under <c>window</c>, so a page's ids never collide with them). It is the same element each
/// time it is asked for, so what is bound on its events stays: <c>on click on #window.bot, call ShowClaude</c>. Its
/// events reached (to bind on them), the screen hands its clicks to it instead of doing their own.
/// </summary>
[PlangType("element")]
public sealed class Element : global::app.type.item.@this, global::app.type.item.ICreate<Element>
{
    /// <summary>Its selector: <c>#window.bot</c>.</summary>
    [LlmBuilder, Out] public string Selector { get; init; } = "";

    /// <summary>The screen it is on.</summary>
    internal Screen? Screen { get; init; }

    /// <summary>One step by dot: <c>on</c> — its events; reached, the screen gives this element its clicks.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
    {
        if (string.Equals(key, "on", StringComparison.OrdinalIgnoreCase))
            Screen?.Display?.Input(new JsonObject { ["ui"] = "bind", ["element"] = Selector }.ToJsonString());
        return base.Get(parent, key);
    }

    /// <summary>It was clicked: what is bound on its click runs, handed the click (the window it was in, where).</summary>
    internal async System.Threading.Tasks.Task Clicked(global::app.data.@this click, global::app.actor.context.@this context)
    {
        var before = await on.click.Before(this, context, click);
        if (before is { Success: false } or { Handled: true }) return;
        await on.click.After(this, before ?? click, context);
    }

    public override string ToString() => Selector;
}

/// <summary>A screen's elements, picked by selector: <c>%!screen.element["#window.bot"]%</c>.</summary>
public sealed class Elements(Screen screen) : global::app.type.item.@this
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Element> held = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The element <paramref name="selector"/> picks — the same one each time.</summary>
    internal Element Of(string selector) => held.GetOrAdd(selector.Trim(), s => new Element { Selector = s, Screen = screen });

    /// <summary>The element already asked for under <paramref name="selector"/>, if any (a click on one nobody asked for
    /// has nothing bound).</summary>
    internal Element? Held(string selector) => held.GetValueOrDefault(selector.Trim());

    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key, bool isIndex)
        => Get(parent, key);

    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => System.Threading.Tasks.ValueTask.FromResult(new global::app.data.@this(key, Of(key), parent: parent));
}
