namespace app.type.root;

/// <summary>
/// The types — <c>%!app.type%</c>: the type named <c>type</c> over the list of every type (a dot picks one by name,
/// <c>%!app.type.image%</c>). Its own member <c>format</c> is the format in play (<c>%!app.type.format%</c>): a kind
/// with a MIME or an extension — the one a goal set, down the goals that called it, else the app's own (plain text
/// unless <c>--format=…</c> named another). Written — <c>set %!app.type.format% to "application/plang"</c> — it is
/// that goal's, and holds for what it calls until it returns. Channels that follow it (the console) write and read
/// in it: plang's own format puts out Data whole, so a program running this plang reads an ask as an ask.
/// </summary>
public sealed class @this(global::app.@this app) : global::app.type.@this<global::app.type.@this, global::app.type.list.@this>(app)
{
    /// <summary>The app's own format, under every goal's: plain text, unless the app was started with another.</summary>
    public global::app.type.kind.@this? Format { get; set; }

    /// <summary>The format in play for <paramref name="context"/>: the nearest goal's that set one, else the app's,
    /// else plain text.</summary>
    public global::app.type.kind.@this FormatOf(global::app.actor.context.@this context)
        => context.call.Format ?? Format ?? list.Mime("text/plain");

    /// <summary>The format <paramref name="named"/> names — by MIME (<c>application/plang</c>), name (<c>json</c>)
    /// or extension (<c>.md</c>); null when no type reads such a format.</summary>
    public global::app.type.kind.@this? Named(string named) => list.Format(named);

    /// <summary>One step by dot: <c>format</c> is the format in play for the asker; any other key as every node's.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => string.Equals(key, "format", System.StringComparison.OrdinalIgnoreCase)
            ? new global::app.data.@this(key, new global::app.type.item.text.@this(Spelled(FormatOf(parent.Context))), parent: parent)
            : await base.Get(parent, key);

    /// <summary><c>format</c> written: the writing goal's format from now until it returns — its call frame's. A
    /// format no type reads is refused, never quiet text.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(string key, bool isIndex, object? value,
        global::app.actor.context.@this context)
    {
        if (isIndex || !string.Equals(key, "format", System.StringComparison.OrdinalIgnoreCase))
            return await base.Set(key, isIndex, value, context);
        var text = value is global::app.data.@this d ? (await d.Value())?.ToString() : value?.ToString();
        if (string.IsNullOrWhiteSpace(text) || Named(text) is not { } kind)
            throw new global::app.error.AppException(new global::app.error.Error(
                $"No format '{text}': name one a type reads — a MIME (application/plang), a name (json) or an extension (.md)", "FormatNotFound", 400));
        if (!context.call.SetFormat(kind))
            Format = kind;   // no goal running (the app starting): the app's own
        return this;
    }

    // a format as it is written: its MIME when it has one (application/plang), else its name
    private static string Spelled(global::app.type.kind.@this kind)
        => kind.Mime.Count > 0 ? kind.Mime[0] : kind.Name;
}
