namespace app.module.browser.type.page;

/// <summary>
/// PLang <c>page</c> value — what a browser shows, given as plang names it: a path from the app's root
/// (<c>/app/writer/start.html</c>; <c>/system/…</c> the system's) or a website's url (<c>https://mbl.is/x#part</c>) —
/// never <c>file://</c>. Two kinds, each born from the path it takes and answering where Chromium finds it (decision
/// 759): a <see cref="local.@this"/> (a file) is read as the one who opens it first — a browser started trusted must not
/// read for a caller what the caller couldn't read itself (finding 1 of 579); a <see cref="web.@this"/> is its url
/// exactly as written, unasked — showing a website is the browser's job, not plang reading the web. Nothing else is a
/// page: <c>about:</c>, <c>chrome://</c>, <c>javascript:</c>, <c>data:</c> are refused (NotAPage).
/// </summary>
[global::app.Attributes.PlangType("page"), global::app.Attributes.Kinds]
public abstract class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>Where Chromium finds it, once the one opening it may see it: its address, or the refusal.</summary>
    internal abstract Task<global::app.data.@this<global::app.type.item.text.@this>> Address(global::app.actor.context.@this context);

    /// <summary>A page from what names it: a path, or its text as a path is read from text; else it declines with why.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (raw is @this page) return page;
        var made = raw switch
        {
            global::app.type.item.path.@this path => Of(path, data.Context!),
            _ when ((raw as global::app.type.item.@this)?.Clr<object>() ?? raw) is string text => Named(text, data.Context!),
            _ => NotAPage($"{raw}", data.Context!),
        };
        if (made.Peek() is @this found) return found;
        data.Fail(made.Error!);
        return null;
    }

    /// <summary>What was typed in an address bar, as a page: a path or an address with its scheme stays; a name with a
    /// dot and no spaces is https; anything else is a search.</summary>
    internal static global::app.data.@this<@this> Typed(string typed, global::app.actor.context.@this context)
    {
        var text = typed.Trim();
        if (!text.StartsWith('/') && SchemeOf(text) == null)
            text = !text.Contains(' ') && text.Contains('.') ? "https://" + text : "https://duckduckgo.com/?q=" + Uri.EscapeDataString(text);
        return Named(text, context);
    }

    // a page from its text: only a file's or a website's scheme is read as a path (a bare text is a file's path); any
    // other scheme names no page — refused, never read as a file named "about:blank"
    private static global::app.data.@this<@this> Named(string text, global::app.actor.context.@this context)
    {
        if (SchemeOf(text) is { } scheme && scheme is not ("file" or "http" or "https")) return NotAPage(text, context);
        try { return Of(global::app.type.item.path.@this.Resolve(text, context), context); }
        catch (global::app.error.AppException ex) { return global::app.data.@this<@this>.From(context.Error(ex.Error)); }
    }

    // the kind that takes this path makes the page
    private static global::app.data.@this<@this> Of(global::app.type.item.path.@this path, global::app.actor.context.@this context)
        => (local.@this.Create(path) ?? (@this?)web.@this.Create(path)) is { } page
            ? context.Ok<@this>(page)
            : NotAPage(path.ToString(), context);

    private static string? SchemeOf(string text)
        => System.Text.RegularExpressions.Regex.Match(text, @"^([a-zA-Z][a-zA-Z0-9+.-]*):") is { Success: true } m
            ? m.Groups[1].Value.ToLowerInvariant() : null;

    private static global::app.data.@this<@this> NotAPage(string what, global::app.actor.context.@this context)
        => global::app.data.@this<@this>.From(context.Error(new global::app.error.ActionError(
            $"'{what}' is not a page: a browser here shows a file (/app/x.html) or a website (https://…).", "NotAPage", 400)));

    public override void Write(global::app.type.format.IWriter writer) => writer.String(ToString());

    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        Write(writer);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
