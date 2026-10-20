namespace app.type.item.path.scheme.file;

/// <summary>The <c>file</c> scheme — a path on the app's disk.</summary>
public sealed class @this : global::app.type.item.path.scheme.@this
{
    public @this(global::app.type.@this owner)
        : base("file", owner, (raw, context) => global::app.type.item.path.file.@this.Resolve(raw, context)) { }
}
