namespace app.type.item.path.scheme.http;

/// <summary>The <c>http</c> scheme — a url.</summary>
public sealed class @this : global::app.type.item.path.scheme.@this
{
    public @this(global::app.type.@this owner)
        : base("http", owner, (raw, context) => global::app.type.item.path.http.@this.Resolve(raw, context)) { }
}
