using app.Utils;

namespace app.type.item.path.file;

/// <summary>
/// FilePath-scheme derivation verbs. Pure path-string math — the OS form via
/// <see cref="PathHelper"/>, the typed form via its own "/" math (<c>typed</c>); none of these touch the filesystem. The
/// FilePath ctor canonicalizes <c>Absolute</c>, so any <c>..</c>
/// segments these verbs introduce via <c>PathHelper.Combine</c> are
/// resolved before the derived path is stored. Each derived path carries typed
/// text derived from this path's typed text by the same math, so it shows as the
/// developer would have written it — never the install root — and holds this path's type.
/// </summary>
public sealed partial class @this
{
    public override global::app.type.item.path.@this Parent
    {
        get
        {
            var dir = PathHelper.GetDirectoryName(Absolute);
            if (string.IsNullOrEmpty(dir)) return this;
            return new @this(dir, Type) { Raw = new typed(Raw).Parent };
        }
    }

    public override global::app.type.item.path.@this WithName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return Parent.Combine(name);
    }

    public override global::app.type.item.path.@this WithExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        return new @this(PathHelper.ChangeExtension(Absolute, extension), Type) { Raw = new typed(Raw).Extension(extension) };
    }

    public override global::app.type.item.path.@this Combine(string child)
    {
        ArgumentException.ThrowIfNullOrEmpty(child);
        return new @this(PathHelper.Combine(Absolute, child), Type) { Raw = new typed(Raw).Combine(child) };
    }

    public override global::app.type.item.path.@this InFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        return Parent.Combine(folder).Combine(Name.ToString());
    }
}
