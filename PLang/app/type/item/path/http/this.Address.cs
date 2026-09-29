namespace app.type.item.path.http;

// An http path made from a written url: the url a request goes to, and how a relative one joins its base.
public sealed partial class @this : global::app.type.item.ICreate<@this>
{
    /// <summary>An http path passes through; anything else is declined, for the courier to make.</summary>
    public static new @this? Create(object? value) => value as @this;

    /// <summary>A written url made an http path: absolute (http or https), or a bare host — <c>https://</c> is
    /// assumed. A relative url (a leading <c>/</c>) needs a base to join (<see cref="Address"/>): alone it is
    /// NoBaseUrl; any other scheme is InvalidUrlScheme.</summary>
    public static new @this? Create(object? value, global::app.data.@this data)
    {
        if (value is @this self) return self;
        if (value is null or global::app.type.item.@null.@this) return null;
        var written = (value as global::app.type.item.@this)?.ToString() ?? value as string;
        if (written == null) return null;
        if (written.StartsWith('/'))
        {
            data.Fail(new global::app.error.Error(
                $"'{written}' is a relative url — it needs a base url to join. Use 'configure http, base url https://...'",
                "NoBaseUrl", 400));
            return null;
        }
        var url = written.Contains("://") ? written : "https://" + written;
        if (!System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            data.Fail(new global::app.error.Error($"Only http:// and https:// URLs are allowed, got '{written}'", "InvalidUrlScheme", 400));
            return null;
        }
        return new @this(url);
    }

    /// <summary>The url <paramref name="url"/> names, with this as its base: a relative one (a leading <c>/</c>) is
    /// joined onto this; any other is an http url of its own.</summary>
    public global::app.data.@this<@this> Address(global::app.type.item.text.@this url, actor.context.@this context)
    {
        var written = url.ToString();
        if (written.StartsWith('/')) return context.Ok<@this>((@this)Combine(written));
        var carrier = new global::app.data.@this<@this>("url", context: context);
        return Create(written, carrier) is { } made ? context.Ok<@this>(made) : carrier;
    }
}
