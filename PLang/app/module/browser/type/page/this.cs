namespace app.module.browser.type.page;

/// <summary>
/// PLang <c>page</c> value — what a browser shows, given as plang names it: a path from the app's root
/// (<c>/app/writer/start.html</c>; <c>/system/…</c> the system's) or a website's url (<c>https://mbl.is/x#part</c>) —
/// never <c>file://</c>. Two kinds, each answering where Chromium finds it (decision 759): a <see cref="local.@this"/> (a file) is
/// read as the one who opens it first — a browser started trusted must not read for a caller what the caller couldn't
/// read itself (finding 1 of 579); a <see cref="web.@this"/> is its url exactly as written, unasked — showing a website
/// is the browser's job, not plang reading the web.
/// </summary>
[global::app.Attributes.PlangType("page"), global::app.Attributes.Kinds]
public abstract class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>Where Chromium finds it, once the one opening it may see it: its address, or the refusal.</summary>
    internal abstract Task<global::app.data.@this<global::app.type.item.text.@this>> Address(global::app.actor.context.@this context);

    /// <summary>A page from what names it: a path (a website's url is a web page, a file's path a file page) or its
    /// text, as a path is read from text; anything else declines with why.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (raw is @this page) return page;
        try
        {
            var path = raw switch
            {
                global::app.type.item.path.@this given => given,
                _ when ((raw as global::app.type.item.@this)?.Clr<object>() ?? raw) is string text
                    => global::app.type.item.path.@this.Resolve(text.Trim(), data.Context!),
                _ => null,
            };
            if (Of(path) is { } made) return made;
            data.Fail(new global::app.error.Error($"A page is a file's path or a website's url, not {raw}.", "NotAPage", 400));
            return null;
        }
        catch (global::app.error.AppException ex)
        {
            data.Fail(ex.Error);
            return null;
        }
    }

    /// <summary>What was typed in an address bar, as a page: a path or a url stays; a name with a dot and no spaces is
    /// https; anything else is a search.</summary>
    internal static @this Typed(string typed, global::app.actor.context.@this context)
    {
        var text = typed.Trim();
        if (!text.StartsWith('/') && !text.Contains("://"))
            text = !text.Contains(' ') && text.Contains('.') ? "https://" + text : "https://duckduckgo.com/?q=" + Uri.EscapeDataString(text);
        return Of(global::app.type.item.path.@this.Resolve(text, context))
            ?? throw new global::app.error.AppException(new global::app.error.Error($"'{typed}' is not a page: a file's path or a website's url.", "NotAPage", 400));
    }

    private static @this? Of(global::app.type.item.path.@this? path) => path switch
    {
        global::app.type.item.path.http.@this site => new web.@this(site.Raw),
        global::app.type.item.path.file.@this file => new local.@this(file),
        _ => null,
    };

    public override void Write(global::app.type.format.IWriter writer) => writer.String(ToString());

    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        Write(writer);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
