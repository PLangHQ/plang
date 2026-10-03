using app.Attributes;

namespace app.module.screen.type.element;

/// <summary>
/// One of a screen's elements, by its selector — <c>%!screen.element["#window.bot"]%</c> (a window's ☰; a window's
/// own parts are named under <c>window</c>, so a page's ids never collide with them). It is the same element each
/// time it is asked for, so what is bound on its events stays: <c>on click on #window.bot, call ShowClaude</c>. Its
/// events reached (to bind on them), the screen hands its clicks to it instead of doing their own.
/// </summary>
[PlangType("element")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    private readonly string _selector;
    private readonly screen.@this _screen;

    internal @this(string selector, screen.@this screen)
    {
        _selector = selector;
        _screen = screen;
    }

    /// <summary>Its selector: <c>#window.bot</c>.</summary>
    [LlmBuilder, Out] public global::app.type.item.text.@this Selector => _selector;

    /// <summary>One step by dot: <c>on</c> — its events; reached, the screen gives this element its clicks.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
    {
        if (string.Equals(key, "on", StringComparison.OrdinalIgnoreCase))
            _screen.Bind(_selector);
        return base.Get(parent, key);
    }

    /// <summary>It was clicked: what is bound on its click runs, handed the click (the window it was in, where).</summary>
    internal async System.Threading.Tasks.Task Clicked(global::app.data.@this click, global::app.actor.context.@this context)
    {
        var before = await on.click.Before(this, context, click);
        if (before is { Success: false } or { Handled: true }) return;
        await on.click.After(this, before ?? click, context);
    }

    public override string ToString() => _selector;
}
