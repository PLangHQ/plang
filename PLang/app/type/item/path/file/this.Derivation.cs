using app.Utils;

namespace app.type.item.path.file;

/// <summary>
/// FilePath-scheme derivation verbs. Pure path-string math via
/// <see cref="PathHelper"/> — none of these touch the filesystem. The
/// FilePath ctor canonicalizes <c>Absolute</c>, so any <c>..</c>
/// segments these verbs introduce via <c>PathHelper.Combine</c> are
/// resolved before the derived path is stored.
/// </summary>
public sealed partial class @this
{
    public override global::app.type.item.path.@this Parent
    {
        get
        {
            var dir = PathHelper.GetDirectoryName(Absolute);
            if (string.IsNullOrEmpty(dir)) return this;
            return new @this(dir, Context);
        }
    }

    public override global::app.type.item.path.@this WithName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var dir = PathHelper.GetDirectoryName(Absolute) ?? "";
        return new @this(PathHelper.Combine(dir, name), Context);
    }

    public override global::app.type.item.path.@this WithExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        return new @this(PathHelper.ChangeExtension(Absolute, extension), Context);
    }

    public override global::app.type.item.path.@this Combine(string child)
    {
        ArgumentException.ThrowIfNullOrEmpty(child);
        return new @this(PathHelper.Combine(Absolute, child), Context);
    }

    public override global::app.type.item.path.@this InFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        var dir = PathHelper.GetDirectoryName(Absolute) ?? "";
        var name = PathHelper.GetFileName(Absolute);
        return new @this(PathHelper.Combine(dir, folder, name), Context);
    }
}
