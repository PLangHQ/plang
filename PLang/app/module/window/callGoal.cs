using System.Text.Json;
using Browser = app.module.browser.Browser;

namespace app.module.window;

/// <summary>
/// Calls one of a window's page's goals — a function of the page, like <c>ShowFiles</c> — the way
/// <c>goal.call</c> calls one of plang's: <c>call ShowFiles files=%files% in %window%</c> runs
/// <c>ShowFiles({files: …})</c> in the page, the arguments as one object, and what it returns (awaited)
/// is <c>%!data%</c>.
/// </summary>
[Action("callGoal", Cacheable = false)]
public partial class callGoal : IContext
{
    /// <summary>The page's goal: the name of a function the page has.</summary>
    [app.Attributes.Goal]
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }

    /// <summary>The arguments — one named row each, as for <c>goal.call</c>; the function gets them as
    /// one object, a property per name.</summary>
    public partial data.@this<global::app.type.item.list.@this>? Parameter { get; init; }

    /// <summary>The window: one from <c>window.open</c>, <c>%browser.desktop%</c>, or its id on the screen (with Browser).</summary>
    public partial data.@this Window { get; init; }

    /// <summary>The browser the window is in, when the window is given by its id.</summary>
    public partial data.@this<Browser>? Browser { get; init; }

    public async Task<data.@this> Start()
    {
        var browser = Browser == null ? null : await Browser.Value();
        if (await global::app.module.window.Window.Of(Window, browser, Context) is not { } window)
            return Context.Error(new global::app.error.ActionError($"No such window: {Window.Peek()}", "WindowNotFound", 404));
        var name = (await Name.Value())?.Clr<string>() ?? "";
        // the arguments as one object: a dict writes itself as json. They leave for the page here, so
        // each is opened to what it holds (a file reference to its content), as a channel does
        var arguments = new global::app.type.item.dict.@this();
        if (Parameter != null && await Parameter.Value() is global::app.type.item.list.@this list)
            foreach (var argument in list.Items(Context))
                if (argument.Peek() is { IsNull: false })
                {
                    var named = await argument.Follow(Context);
                    arguments.Set(new data.@this(argument.Name, await named.Value(), context: Context));
                }
        using var written = new MemoryStream();
        await global::app.type.item.text.@this.Encode(written, new data.@this("arguments", arguments, context: Context), Context, null, null, CancellationToken.None);
        JsonElement reply;
        try { reply = await window.Call(name, System.Text.Encoding.UTF8.GetString(written.ToArray())); }
        catch (Exception ex) when (ex is TimeoutException or System.Net.WebSockets.WebSocketException)
        {
            return Context.Error(new global::app.error.ActionError($"The page's goal {name} didn't answer: {ex.Message}", "PageGoalTimeout", 504));
        }
        return Answered(reply, name);
    }

    /// <summary>What the page's goal returned, as plang's value; what it threw, as an error.</summary>
    private data.@this Answered(JsonElement reply, string name)
    {
        if (!reply.TryGetProperty("result", out var outer))
            return Context.Error(new global::app.error.ActionError($"The page's goal {name} failed: {reply}", "PageGoalFailed", 500));
        if (outer.TryGetProperty("exceptionDetails", out var thrown))
        {
            var why = thrown.TryGetProperty("exception", out var ex) && ex.TryGetProperty("description", out var d) ? d.GetString() : thrown.GetProperty("text").GetString();
            return Context.Error(new global::app.error.ActionError($"The page's goal {name} failed: {why}", "PageGoalFailed", 500));
        }
        if (!outer.TryGetProperty("result", out var result) || !result.TryGetProperty("value", out var value))
            return Context.Ok();
        return Context.Ok(new global::app.type.item.serializer.json(Context).Parse(value.Clone()));
    }
}
