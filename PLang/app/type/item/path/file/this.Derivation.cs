using app.Utils;

namespace app.type.item.path.file;

/// <summary>
/// FilePath-scheme derivation verbs. Pure path-string math via
/// <see cref="PathHelper"/> — none of these touch the filesystem. The
/// FilePath ctor canonicalizes <c>Absolute</c>, so any <c>..</c>
/// segments these verbs introduce via <c>PathHelper.Combine</c> are
/// resolved before the derived path is stored. Each derived path carries typed
/// text derived from this path's typed text by the same math, so it shows as the
/// developer would have written it — never the install root.
/// </summary>
public sealed partial class @this
{
    public override global::app.type.item.path.@this Parent
    {
        get
        {
            var dir = PathHelper.GetDirectoryName(Absolute);
            if (string.IsNullOrEmpty(dir)) return this;
            return new @this(dir) { Raw = TypedParent };
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
        return new @this(PathHelper.ChangeExtension(Absolute, extension)) { Raw = PathHelper.ChangeExtension(Raw, extension) };
    }

    public override global::app.type.item.path.@this Combine(string child)
    {
        ArgumentException.ThrowIfNullOrEmpty(child);
        // Under "." (the goal folder) the typed child is the child itself.
        return new @this(PathHelper.Combine(Absolute, child))
            { Raw = Raw is "" or "." ? child : PathHelper.Combine(Raw, child) };
    }

    public override global::app.type.item.path.@this InFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        return Parent.Combine(folder).Combine(FileName);
    }

    // The typed text's parent — "data/file.txt" → "data", "/data/file.txt" → "/data". A bare
    // name's parent is the goal folder it resolves against: ".".
    private string TypedParent => PathHelper.GetDirectoryName(Raw) is { Length: > 0 } dir ? dir : ".";
}
