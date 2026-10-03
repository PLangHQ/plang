using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using app.Attributes;
using Data = global::app.data.@this;
using Context = global::app.actor.context.@this;

namespace app.module.window.type.window;

/// <summary>
/// A window on the screen, showing a page: the desktop, or one <c>window.open</c> opened. It goes where it is sent
/// (<c>window.navigate</c>), gives its page messages (<c>window.post</c>), calls its page's goals (<c>window.call</c>),
/// shows how it looks (<c>window.screenshot</c>, an image), loads again (<c>window.reload</c>) and closes. A window just
/// opened is not on the screen yet: it is shown when its page is — until then what it is asked to do waits. Each verb
/// answers as itself: what came of it, or why not.
/// </summary>
[PlangType("window")]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
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

    /// <summary>The window a step names: a window (<c>%window%</c>), or its id on the screen in
    /// <paramref name="browser"/> (<c>window %event.id% … in %browser%</c>) — or no such window. A variable is followed
    /// to what it names first.</summary>
    internal static async Task<global::app.data.@this<@this>> Of(Data named,
        global::app.data.@this<global::app.module.browser.type.browser.@this>? browser, Context context)
    {
        var holder = browser == null ? null : await browser.Value();
        var window = await (await named.Follow(context)).Value() switch
        {
            @this one => one,
            // an id: from an event it may arrive as any number, or as its text — a window of a browser on a screen
            { } id when long.TryParse(id.ToString(), out var number) => (holder as global::app.module.browser.type.browser.screen.@this)?.window.ById(number),
            _ => null,
        };
        return window != null ? context.Ok<@this>(window)
            : global::app.data.@this<@this>.From(context.Error(new global::app.error.ActionError($"No such window: {named.Peek()}", "WindowNotFound", 404)));
    }

    /// <summary>Goes where <paramref name="url"/> says (<c>window.navigate</c>) — nowhere when it says nothing.</summary>
    internal async Task<Data> Navigate(global::app.data.@this<global::app.type.item.text.@this> url, Context context)
        => await url.Value() is global::app.type.item.text.@this typed
            ? await Navigate(typed.Clr<string>() ?? "", context)
            : context.Error(new global::app.error.ActionError($"Nowhere to go: Url is {url.Peek()}", "UrlMissing", 400));

    /// <summary>Goes to what was typed: an address (<c>mbl.is</c> is <c>https://mbl.is</c>), or words to search for. A
    /// <c>file://</c> page is read as the one who sends it there.</summary>
    internal async Task<Data> Navigate(string typed, Context context)
    {
        var url = Where(typed);
        if (await global::app.module.browser.type.browser.@this.Readable(url, context) is { } refused) return refused;
        await (await Page()).Navigate(url);
        return context.Ok();
    }

    /// <summary>Gives its page <paramref name="message"/>: a <c>message</c> event, origin <c>"plang"</c>, its data the
    /// message as text (a dict or list as its json).</summary>
    internal async Task<Data> Post(Data message, Context context)
    {
        using var text = new MemoryStream();
        await global::app.type.item.text.@this.Encode(text, message, context, null, null, CancellationToken.None);
        await (await Page()).Post(System.Text.Encoding.UTF8.GetString(text.ToArray()));
        return context.Ok();
    }

    /// <summary>How its page looks now: a PNG image.</summary>
    internal async Task<Data> Screenshot(Context context)
    {
        try
        {
            var png = await (await Page()).Screenshot();
            return png.Length == 0
                ? context.Error(new global::app.error.ActionError("The page gave no picture.", "NoScreenshot", 500))
                : context.Ok<global::app.type.item.image.@this>(new global::app.type.item.image.@this(Convert.FromBase64String(png), "image/png"));
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or FormatException)
        {
            return context.Error(new global::app.error.ActionError($"The page didn't give its picture: {ex.Message}", "ScreenshotTimeout", 504));
        }
    }

    /// <summary>Its page loads again from its files, nothing cached — done when the new page has loaded.</summary>
    internal async Task<Data> Reload(Context context)
    {
        try { await (await Page()).Reload(); }
        catch (TimeoutException ex)
        {
            return context.Error(new global::app.error.ActionError($"Window {this} reloaded, but {ex.Message}", "PageNotLoaded", 504));
        }
        return context.Ok();
    }

    /// <summary>Runs its page's goal <paramref name="name"/> (a function of the page) with <paramref name="parameter"/> —
    /// one named row each, as for <c>goal.call</c>, given to the function as one object. What it returned (awaited) is
    /// the answer; what it threw, the error. A page's goal may wait for the person: it has ten minutes, as a goal would.</summary>
    internal async Task<Data> Call(string name, global::app.data.@this<global::app.type.item.list.@this>? parameter, Context context)
    {
        // the arguments as one object: a dict writes itself as json. They leave for the page here, so each is opened
        // to what it holds (a file reference to its content), as a channel does
        var arguments = new global::app.type.item.dict.@this();
        if (parameter != null && await parameter.Value() is global::app.type.item.list.@this list)
            foreach (var argument in list.Items(context))
                if (argument.Peek() is { IsNull: false })
                {
                    var named = await argument.Follow(context);
                    arguments.Set(new Data(argument.Name, await named.Value(), context: context));
                }
        using var written = new MemoryStream();
        await global::app.type.item.text.@this.Encode(written, new Data("arguments", arguments, context: context), context, null, null, CancellationToken.None);
        var goal = JsonValue.Create(name).ToJsonString();
        JsonElement reply;
        try
        {
            reply = await (await Page()).Evaluate(
                $"(async()=>{{const goal=window[{goal}];if(typeof goal!=='function')throw new Error('The page has no goal '+{goal});" +
                $"return await goal({System.Text.Encoding.UTF8.GetString(written.ToArray())});}})()", TimeSpan.FromMinutes(10));
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            return context.Error(new global::app.error.ActionError($"The page's goal {name} didn't answer: {ex.Message}", "PageGoalTimeout", 504));
        }
        if (!reply.TryGetProperty("result", out var outer))
            return context.Error(new global::app.error.ActionError($"The page's goal {name} failed: {reply}", "PageGoalFailed", 500));
        if (outer.TryGetProperty("exceptionDetails", out var thrown))
        {
            var why = thrown.TryGetProperty("exception", out var ex) && ex.TryGetProperty("description", out var d) ? d.GetString() : thrown.GetProperty("text").GetString();
            return context.Error(new global::app.error.ActionError($"The page's goal {name} failed: {why}", "PageGoalFailed", 500));
        }
        if (!outer.TryGetProperty("result", out var result) || !result.TryGetProperty("value", out var value))
            return context.Ok();
        return context.Ok(new global::app.type.item.serializer.json(context).Parse(value.Clone()));
    }

    /// <summary>Closes: its page closes, and with it the window.</summary>
    internal async Task<Data> Close(Context context)
    {
        await (await Page()).Shut();
        return context.Ok();
    }

    /// <summary>The screen says it closed: its connection goes.</summary>
    internal void Gone() => page?.Close();

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
