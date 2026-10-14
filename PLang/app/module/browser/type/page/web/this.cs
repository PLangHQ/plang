namespace app.module.browser.type.page.web;

/// <summary>
/// A page that is a website: its url exactly as written — its query and its <c>#part</c> kept. Showing it is the
/// browser's job, not plang reading the web: nothing is asked.
/// </summary>
public sealed class @this : page.@this
{
    private readonly string _url;

    public @this(string url) => _url = url;

    /// <summary>Its url, as written.</summary>
    [global::app.Out] public global::app.type.item.text.@this Url => _url;

    internal override Task<global::app.data.@this<global::app.type.item.text.@this>> Address(global::app.actor.context.@this context)
        => Task.FromResult(context.Ok<global::app.type.item.text.@this>(_url));

    public override string ToString() => _url;
}
