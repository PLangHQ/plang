namespace app.types.path.file;

/// <summary>
/// FilePath-scheme derivation verbs (D1). OS-path semantics via
/// <c>System.IO.Path</c> string operations — none of these touch the
/// filesystem, so PLNG002 allowlists this file (System.IO use here is pure
/// path arithmetic, no IO).
/// </summary>
public sealed partial class @this
{
    public override global::app.types.path.@this Parent
    {
        get
        {
            var dir = System.IO.Path.GetDirectoryName(_absolutePath);
            if (string.IsNullOrEmpty(dir)) return this;
            return new @this(dir, Context);
        }
    }

    public override global::app.types.path.@this WithName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var dir = System.IO.Path.GetDirectoryName(_absolutePath) ?? "";
        return new @this(System.IO.Path.Combine(dir, name), Context);
    }

    public override global::app.types.path.@this WithExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        return new @this(System.IO.Path.ChangeExtension(_absolutePath, extension), Context);
    }

    public override global::app.types.path.@this Combine(string child)
    {
        ArgumentException.ThrowIfNullOrEmpty(child);
        return new @this(System.IO.Path.Combine(_absolutePath, child), Context);
    }

    public override global::app.types.path.@this InFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        var dir = System.IO.Path.GetDirectoryName(_absolutePath) ?? "";
        var name = System.IO.Path.GetFileName(_absolutePath);
        return new @this(System.IO.Path.Combine(dir, folder, name), Context);
    }
}
