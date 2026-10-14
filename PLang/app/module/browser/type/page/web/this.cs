namespace app.module.browser.type.page.web;

/// <summary>
/// A page that is a website: its url as written — its query and its <c>#part</c> kept. Showing it is the browser's
/// job, not plang reading the web: nothing is asked.
/// </summary>
public sealed class @this : page.@this
{
    private readonly global::app.type.item.path.http.@this _site;

    private @this(global::app.type.item.path.http.@this site) => _site = site;

    /// <summary>The page a website's url is; another path is not this kind's.</summary>
    internal static @this? Create(global::app.type.item.path.@this path)
        => path is global::app.type.item.path.http.@this site ? new @this(site) : null;

    /// <summary>Its url, as plang holds it.</summary>
    [global::app.Out] public global::app.type.item.path.@this Url => _site;

    internal override Task<global::app.data.@this<global::app.type.item.text.@this>> Address(global::app.actor.context.@this context)
        => Task.FromResult(context.Ok<global::app.type.item.text.@this>(_site.Raw));

    public override string ToString() => _site.Raw;
}
