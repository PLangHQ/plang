using app.Utils;

namespace app.types.path.file;

/// <summary>
/// FilePath-scheme derivation verbs (D1). Pure path-string math via
/// <see cref="PathHelper"/> — none of these touch the filesystem. The
/// canonicalization done by the <see cref="@this"/> ctor (security F1
/// fix) means any <c>..</c> segments in the derived results are resolved
/// before <c>_absolutePath</c> is stored, so <c>IsInRoot</c>/<c>Equals</c>
/// can't be fooled.
/// </summary>
public sealed partial class @this
{
    public override global::app.types.path.@this Parent
    {
        get
        {
            var dir = PathHelper.GetDirectoryName(_absolutePath);
            if (string.IsNullOrEmpty(dir)) return this;
            return new @this(dir, Context);
        }
    }

    public override global::app.types.path.@this WithName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var dir = PathHelper.GetDirectoryName(_absolutePath) ?? "";
        return new @this(PathHelper.Combine(dir, name), Context);
    }

    public override global::app.types.path.@this WithExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        return new @this(PathHelper.ChangeExtension(_absolutePath, extension), Context);
    }

    public override global::app.types.path.@this Combine(string child)
    {
        ArgumentException.ThrowIfNullOrEmpty(child);
        return new @this(PathHelper.Combine(_absolutePath, child), Context);
    }

    public override global::app.types.path.@this InFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        var dir = PathHelper.GetDirectoryName(_absolutePath) ?? "";
        var name = PathHelper.GetFileName(_absolutePath);
        return new @this(PathHelper.Combine(dir, folder, name), Context);
    }
}
