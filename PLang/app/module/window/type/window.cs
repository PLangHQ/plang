using System.Text.Json;
using System.Text.RegularExpressions;
using app.Attributes;

namespace app.module.window;

/// <summary>
/// A window on the screen, showing a page: the desktop, or one <c>window.open</c> opened. It goes
/// where it is sent (<c>window.navigate</c>), gives its page messages (<c>window.post</c>), calls its
/// page's goals (<c>window.callGoal</c>) and closes. A window just opened is not on the screen yet: it
/// is shown when its page is — until then what it is asked to do waits.
/// </summary>
[PlangType("window")]
public sealed partial class Window : global::app.type.item.@this, global::app.type.item.ICreate<Window>
{
    private readonly TaskCompletionSource shown = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Page? page;

    /// <summary>Its id on the screen (0 is the desktop); -1 until it is shown.</summary>
    [LlmBuilder, Out] public global::app.type.item.number.@this Id => Number;

    /// <summary>The page it shows.</summary>
    [LlmBuilder, Out] public global::app.type.item.text.@this Url => new(Address);

    /// <summary>Its title.</summary>
    [LlmBuilder, Out] public global::app.type.item.text.@this Title => new(Named);

    internal long Number { get; private set; } = -1;
    /// <summary>DevTools' id for its page, once shown.</summary>
    internal string? Target => page?.Target;
    internal string Address { get; set; } = "";
    internal string Named { get; set; } = "";

    /// <summary>The window is on the screen with its page: what it is asked to do goes to that page.</summary>
    internal async Task Show(long number, Page shows, Func<string, Task>? hear)
    {
        Number = number;
        page = shows;
        await shows.Open(number, hear);
        shown.TrySetResult();
    }

    /// <summary>Its page, once it is shown (a window just opened waits for it, up to 30 seconds).</summary>
    private async Task<Page> Page()
    {
        await shown.Task.WaitAsync(TimeSpan.FromSeconds(30));
        return page!;
    }

    /// <summary>Goes to what was typed: an address (<c>mbl.is</c> is <c>https://mbl.is</c>), or words to search for.</summary>
    internal async Task Navigate(string typed) => await (await Page()).Navigate(Where(typed));

    internal async Task Post(string text) => await (await Page()).Post(text);

    /// <summary>Runs the page's goal <paramref name="name"/> with <paramref name="arguments"/> (one
    /// json object); what it returned, or threw, as DevTools answers it.</summary>
    internal async Task<JsonElement> Call(string name, string arguments)
    {
        var goal = JsonSerializer.Serialize(name);
        return await (await Page()).Evaluate(
            $"(async()=>{{const goal=window[{goal}];if(typeof goal!=='function')throw new Error('The page has no goal '+{goal});" +
            $"return await goal({arguments});}})()");
    }

    /// <summary>Closes: its page closes, and with it the window.</summary>
    internal async Task Close() => await (await Page()).Shut();

    /// <summary>The screen says it closed: its connection goes.</summary>
    internal void Gone() => page?.Close();

    /// <summary>The window a step names: a window (<c>%window%</c>), or its id on the screen in
    /// <paramref name="browser"/> (<c>window %event.id% … in %browser%</c>). A variable is followed to
    /// what it names first.</summary>
    internal static async Task<Window?> Of(data.@this named, global::app.module.browser.Browser? browser, actor.context.@this context)
        => await (await named.Follow(context)).Value() switch
        {
            Window window => window,
            // an id: from an event it may arrive as any number, or as its text
            { } id when long.TryParse(id.ToString(), out var number) => browser?.Windows.ById(number),
            _ => null,
        };

    /// <summary>What was typed, as an address: one with a scheme stays; a name with a dot and no
    /// spaces is https; anything else is a search.</summary>
    private static string Where(string typed)
    {
        var text = typed.Trim();
        if (Regex.IsMatch(text, @"^[a-zA-Z][a-zA-Z0-9+.-]*:")) return text;
        if (!text.Contains(' ') && text.Contains('.')) return "https://" + text;
        return "https://duckduckgo.com/?q=" + Uri.EscapeDataString(text);
    }

    public override string ToString() => $"window {(Number < 0 ? "(opening)" : Number.ToString())} {Address}";
}
