namespace app.type.item.path.scheme.https;

/// <summary>The <c>https</c> scheme — a url, read as an http path.</summary>
public sealed class @this : global::app.type.item.path.scheme.@this
{
    public @this(global::app.type.@this owner)
        : base("https", owner, (raw, context) => global::app.type.item.path.http.@this.Resolve(raw, context)) { }
}
