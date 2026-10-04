using System.Text.Json;
using System.Text.Json.Nodes;
using Cdp = app.module.browser.type.cdp.@this;
using Session = app.module.browser.type.cdp.session.@this;

namespace app.module.window;

/// <summary>
/// The page a window shows, over a DevTools session of its own on the browser's pipe. It goes where it is sent,
/// evaluates, and gives a message event. When it is one of plang's own pages (a file under the app's folder or the os
/// folder), it also gets <c>plang(text)</c>: what it says is heard with its window's id added (<c>"from"</c>). Every
/// call is checked: a page that has since gone somewhere else (a website) is not heard.
/// </summary>
internal sealed class Page(string target, Cdp cdp, Func<string, bool> own)
{
    private Session? _session;

    /// <summary>DevTools' id for this page.</summary>
    internal string Target { get; } = target;

    /// <summary>Attaches. Given <paramref name="hear"/>, the page gets <c>plang(text)</c> and what it says goes there,
    /// <c>"from"</c> <paramref name="window"/>. Given <paramref name="video"/>, any page's video the host plays itself
    /// (pass-through, os/system/browser/video.js) reports to it through <c>plangVideo(json)</c>; the script itself
    /// comes with <see cref="Video"/>, once the host has said what it decodes.</summary>
    internal async Task Open(long window, Func<string, Task>? hear, Func<string, Task>? video = null)
    {
        _session = await cdp.Attach(Target);
        if (hear == null && video == null) return;
        _session.Heard += (method, parameters) =>
            method != "Runtime.bindingCalled" ? Task.CompletedTask
            : parameters.GetProperty("name").GetString() switch
            {
                "plang" when hear != null => Called(window, parameters, hear),
                "plangVideo" when video != null => video(parameters.GetProperty("payload").GetString() ?? ""),
                _ => Task.CompletedTask,
            };
        await Ask("Runtime.enable", new JsonObject());
        if (hear != null) await Ask("Runtime.addBinding", new JsonObject { ["name"] = "plang" });
        if (video != null) await Ask("Runtime.addBinding", new JsonObject { ["name"] = "plangVideo" });
    }

    /// <summary>Every document this page loads from now on runs <paramref name="script"/> before its own (the video
    /// pass-through hook); the one showing now gets it when it loads again.</summary>
    internal Task Video(string script)
        => Ask("Page.addScriptToEvaluateOnNewDocument", new JsonObject { ["source"] = script });

    /// <summary>Goes to <paramref name="url"/>.</summary>
    internal Task Navigate(string url) => Ask("Page.navigate", new JsonObject { ["url"] = url });

    /// <summary>A message to the page: a <c>message</c> event, origin <c>"plang"</c>.</summary>
    internal Task Post(string text) => Ask("Runtime.evaluate", new JsonObject
    {
        ["expression"] = "window.dispatchEvent(new MessageEvent('message',{data:" + JsonValue.Create(text).ToJsonString() + ",origin:'plang'}))",
    });

    /// <summary>Runs <paramref name="expression"/> in the page, a promise awaited (as long as <paramref name="within"/>
    /// allows: a page's goal may wait for the person, a question's answer); DevTools' answer.</summary>
    internal Task<JsonElement> Evaluate(string expression, TimeSpan? within = null) => Ask("Runtime.evaluate", new JsonObject
    {
        ["expression"] = expression, ["awaitPromise"] = true, ["returnByValue"] = true,
    }, within);

    /// <summary>The page as it looks now: a PNG, base64.</summary>
    internal async Task<string> Screenshot()
    {
        var answer = await Ask("Page.captureScreenshot", new JsonObject { ["format"] = "png" }, TimeSpan.FromSeconds(15));
        return answer.TryGetProperty("result", out var r) && r.TryGetProperty("data", out var data) ? data.GetString() ?? "" : "";
    }

    /// <summary>The page loads again, from its files (nothing cached): a change to them shows. Done when the new
    /// page has loaded (its scripts ran, its goals are there) — not when the reload started: the next step talks
    /// to the page (calls its goal, takes its picture). A page that hasn't loaded in ten seconds is said.</summary>
    internal async Task Reload()
    {
        // the old document answers "complete" until it goes: wait for one that is new (a fresh mark it doesn't have)
        await Ask("Runtime.evaluate", new JsonObject { ["expression"] = "window.__reloading = true" });
        await Ask("Page.reload", new JsonObject { ["ignoreCache"] = true });
        var until = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < until)
        {
            try
            {
                var reply = await Ask("Runtime.evaluate", new JsonObject
                {
                    ["expression"] = "!window.__reloading && document.readyState === 'complete'", ["returnByValue"] = true,
                });
                if (reply.TryGetProperty("result", out var r) && r.TryGetProperty("result", out var v)
                    && v.TryGetProperty("value", out var loaded) && loaded.ValueKind == JsonValueKind.True) return;
            }
            catch (TimeoutException) { }   // between documents: nothing answers yet
            await Task.Delay(100);
        }
        throw new TimeoutException("the page didn't finish loading again within 10 seconds");
    }

    /// <summary>The page closes (and the window it is in).</summary>
    internal Task Shut() => cdp.Ask("Target.closeTarget", new JsonObject { ["targetId"] = Target });

    /// <summary>The session goes; the page is gone already (its window closed).</summary>
    internal void Close()
    {
        if (_session is { } session) _ = session.Detach();
    }

    // the page called plang(text): heard when the page that called is still one of plang's own
    private async Task Called(long window, JsonElement parameters, Func<string, Task> hear)
    {
        var said = parameters.GetProperty("payload").GetString() ?? "";
        var context = parameters.GetProperty("executionContextId").GetInt32();
        if (await Ours(context)) await hear(From(window, said));
    }

    /// <summary>The page that called is still one of the app's own (not a website the window went to).</summary>
    private async Task<bool> Ours(int context)
    {
        try
        {
            var reply = await Ask("Runtime.evaluate", new JsonObject { ["expression"] = "location.href", ["contextId"] = context, ["returnByValue"] = true });
            return reply.TryGetProperty("result", out var r) && r.TryGetProperty("result", out var v)
                && v.TryGetProperty("value", out var href) && href.ValueKind == JsonValueKind.String
                && own(href.GetString()!);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or KeyNotFoundException) { return false; }
    }

    /// <summary>What the page said, with its window's id: a json object gets <c>"from"</c>; anything else stays as it is.</summary>
    private static string From(long window, string said)
    {
        try
        {
            if (JsonNode.Parse(said) is JsonObject o)
            {
                o["from"] = window;
                return o.ToJsonString();
            }
        }
        catch (JsonException) { }
        return said;
    }

    private Task<JsonElement> Ask(string method, JsonObject parameters, TimeSpan? within = null)
        => (_session ?? throw new InvalidOperationException($"the page {Target} isn't attached")).Ask(method, parameters, within);
}
