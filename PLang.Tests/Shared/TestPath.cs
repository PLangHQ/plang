namespace PLang.Tests;

/// <summary>A path made the way a program's makers make one: born holding the app's <c>{path, &lt;scheme&gt;}</c>, as
/// <paramref name="context"/>'s types hold it.</summary>
public static class TestPath
{
    /// <summary>A file path at <paramref name="absolute"/>, as written (no resolving against a goal folder).</summary>
    public static global::app.type.item.path.file.@this File(this global::app.actor.context.@this context, string absolute)
        => new(absolute, context.App.type.list[new global::app.type.@this("path", "file"), context]);

    /// <summary>A url, of the scheme it is written with (http or https).</summary>
    public static global::app.type.item.path.http.@this Url(this global::app.actor.context.@this context, string url)
        => new(url, context.App.type.list[new global::app.type.@this("path",
            url.StartsWith("https:", System.StringComparison.OrdinalIgnoreCase) ? "https" : "http"), context]);
}
