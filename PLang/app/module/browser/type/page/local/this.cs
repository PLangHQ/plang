namespace app.module.browser.type.page.local;

/// <summary>
/// A page that is a file here: plang's path for it (<c>/app/writer/start.html</c>). Chromium takes a file by its raw
/// address, a take-over like sqlite's: the file is read as the one who opens it first, and only then is its address
/// given.
/// </summary>
public sealed class @this : page.@this
{
    private readonly global::app.type.item.path.file.@this _path;

    public @this(global::app.type.item.path.file.@this path) => _path = path;

    /// <summary>The file, as plang names it.</summary>
    [global::app.Out] public global::app.type.item.path.@this Path => _path;

    internal override async Task<global::app.data.@this<global::app.type.item.text.@this>> Address(global::app.actor.context.@this context)
    {
        var allowed = await _path.Authorize(global::app.type.item.permission.Verb.read, context);
        if (!allowed.Success || allowed.Exits) return global::app.data.@this<global::app.type.item.text.@this>.From(allowed);
        return context.Ok<global::app.type.item.text.@this>(new Uri(_path.Absolute).AbsoluteUri);
    }

    public override string ToString() => _path.Raw;
}
