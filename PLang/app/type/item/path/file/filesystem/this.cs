using app.Utils;
using StatInfo = global::app.type.item.path.@this.StatInfo;

namespace app.type.item.path.file.filesystem;

/// <summary>
/// The disk: what a file path's verbs read, write and list, after their gate. The one place a file path meets
/// <c>System.IO</c>. It asks nothing — every caller is a path verb that has already passed its
/// <c>AuthGate</c>. A missing file reads as the IO exception the verbs already answer.
/// </summary>
public class @this
{
    // its folders, each where the OS lands for it, read on first ask and kept (an overlay holds its disk's)
    private readonly Lazy<global::app.type.item.path.file.@this> _root, _os, _store;

    /// <summary>The disk of the app whose folder is <paramref name="folder"/>: what <c>/</c> is, beside the runtime's os
    /// folder and the app's stores' folder.</summary>
    public @this(string folder)
    {
        _root = new(() => Land(folder));
        _os = new(() => Land(PathHelper.GetFullPath(PathHelper.Combine(AppContext.BaseDirectory, "os"))));
        _store = new(() => Land(PathHelper.Combine(folder, global::app.store.@this.Folder)));
    }

    /// <summary>A filesystem over <paramref name="under"/>: its folders are the ones under it.</summary>
    protected @this(@this under) => (_root, _os, _store) = (under._root, under._os, under._store);

    /// <summary>The app's folder — what <c>/</c> is — where the OS lands for it: <c>%!app.filesystem.root%</c>.</summary>
    [global::app.LlmBuilder]
    public global::app.type.item.path.file.@this root => _root.Value;

    /// <summary>The runtime's os folder — what <c>/system/</c> falls back to — where the OS lands for it:
    /// <c>%!app.filesystem.os%</c>.</summary>
    [global::app.LlmBuilder]
    public global::app.type.item.path.file.@this os => _os.Value;

    /// <summary>The app's stores' folder (<c>&lt;root&gt;/.data</c>), there yet or not — where the OS lands for it.</summary>
    internal global::app.type.item.path.file.@this store => _store.Value;

    // lands one of its folders: where the OS lands for it
    private global::app.type.item.path.file.@this Land(string folder)
    {
        var at = new file.@this(folder, new global::app.type.@this("path", typeof(file.@this), "file"));
        return at.Landing(this, last: true) is { } landed ? Long(landed) : at;
    }

    /// <summary><paramref name="path"/> with each part as the OS holds its name: on Windows an 8.3 short part
    /// (<c>DATA~1</c>) is its long name, in its real case, for the part of the path that exists; elsewhere the path
    /// itself.</summary>
    internal global::app.type.item.path.file.@this Long(global::app.type.item.path.file.@this path)
    {
        if (!OperatingSystem.IsWindows()) return path;
        // the deepest part that exists is what the OS can name; what is under it is written as asked
        var at = path.Absolute;
        var rest = new Stack<string>();
        while (!System.IO.Directory.Exists(at) && !System.IO.File.Exists(at))
        {
            if (PathHelper.GetDirectoryName(at) is not { } parent) return path;
            rest.Push(PathHelper.GetFileName(at));
            at = parent;
        }
        var buffer = new char[32768];
        var length = GetLongPathNameW(at, buffer, (uint)buffer.Length);
        if (length == 0 || length > buffer.Length) return path;
        return new file.@this(PathHelper.Combine([new string(buffer, 0, (int)length), .. rest]), path.Type);
    }

    // The OS boundary's own call (kernel32): a path's long form, every short part expanded. Windows only.
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathNameW(string shortPath, char[] longPath, uint size);

    public virtual bool IsFile(file.@this p) => System.IO.File.Exists(p.Absolute);

    public virtual bool IsFolder(file.@this p) => System.IO.Directory.Exists(p.Absolute);

    public virtual Task<byte[]> Read(file.@this p) => System.IO.File.ReadAllBytesAsync(p.Absolute);

    /// <summary>Writes the file whole, making its folder when there is none.</summary>
    public virtual async Task Write(file.@this p, byte[] content)
    {
        Parent(p);
        await System.IO.File.WriteAllBytesAsync(p.Absolute, content);
    }

    /// <summary>Writes the file from <paramref name="content"/> as it is read, never held whole, making its folder when
    /// there is none.</summary>
    public virtual async Task Write(file.@this p, System.IO.Stream content, CancellationToken ct = default)
    {
        Parent(p);
        await using var file = System.IO.File.Create(p.Absolute);
        await content.CopyToAsync(file, ct);
    }

    /// <summary>The file, to be read as it streams.</summary>
    public virtual Task<System.IO.Stream> Open(file.@this p) => Task.FromResult<System.IO.Stream>(System.IO.File.OpenRead(p.Absolute));

    public virtual async Task Append(file.@this p, string text)
    {
        Parent(p);
        await System.IO.File.AppendAllTextAsync(p.Absolute, text);
    }

    /// <summary>Removes the file, or the folder — with what it holds when <paramref name="recursive"/>. A link is
    /// removed itself, never what it leads to.</summary>
    public virtual void Delete(file.@this p, bool recursive)
    {
        // a link to a folder (a junction too) is removed as a folder is, alone: a file's delete refuses it on Windows
        if (Link(p) != null) { if (IsFolder(p)) System.IO.Directory.Delete(p.Absolute, recursive: false); else System.IO.File.Delete(p.Absolute); }
        else if (IsFile(p)) System.IO.File.Delete(p.Absolute);
        else System.IO.Directory.Delete(p.Absolute, recursive);
    }

    /// <summary>What the link at <paramref name="p"/> leads to, as it is written — the link itself read, never followed;
    /// null when <paramref name="p"/> is no link.</summary>
    public virtual string? Link(file.@this p) => new System.IO.FileInfo(p.Absolute).LinkTarget;

    /// <summary>Makes a link at <paramref name="p"/> leading to <paramref name="target"/>, as it is written, making its
    /// folder when there is none.</summary>
    public virtual void Link(file.@this p, string target)
    {
        Parent(p);
        System.IO.File.CreateSymbolicLink(p.Absolute, target);
    }

    /// <summary>Sets who may read, write and run what is at <paramref name="p"/>.</summary>
    public virtual void Mode(file.@this p, System.IO.UnixFileMode mode) => System.IO.File.SetUnixFileMode(p.Absolute, mode);

    /// <summary>Makes the folder, and the folders above it.</summary>
    public virtual void Create(file.@this p) => System.IO.Directory.CreateDirectory(p.Absolute);

    /// <summary>The files and folders in <paramref name="folder"/> whose name matches <paramref name="pattern"/>;
    /// those under its folders too when <paramref name="recursive"/>. Each holds the folder's type.</summary>
    public virtual IEnumerable<file.@this> List(file.@this folder, string pattern, bool recursive)
        => System.IO.Directory.EnumerateFileSystemEntries(folder.Absolute, pattern,
                recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly)
            .Select(entry => new file.@this(entry, folder.Type));

    /// <summary>Moves the file, or the folder whole — over what is at <paramref name="to"/> when
    /// <paramref name="overwrite"/>.</summary>
    public virtual Task Move(file.@this from, file.@this to, bool overwrite)
    {
        Parent(to);
        if (IsFile(from)) System.IO.File.Move(from.Absolute, to.Absolute, overwrite);
        else
        {
            if (overwrite && IsFolder(to)) System.IO.Directory.Delete(to.Absolute, recursive: true);
            System.IO.Directory.Move(from.Absolute, to.Absolute);
        }
        return Task.CompletedTask;
    }

    /// <summary>Copies the file.</summary>
    public virtual Task Copy(file.@this from, file.@this to, bool overwrite)
    {
        Parent(to);
        System.IO.File.Copy(from.Absolute, to.Absolute, overwrite);
        return Task.CompletedTask;
    }

    /// <summary>A file said to be at <paramref name="p"/> — with <paramref name="content"/>, or empty when what it
    /// holds is not known yet. The disk holds only what is written: nothing changes here. An overlay holds it.</summary>
    public virtual void Add(file.@this p, byte[]? content = null) { }

    /// <summary>A file or folder said to be gone from <paramref name="p"/>. The disk loses only what is deleted:
    /// nothing changes here. An overlay hides it.</summary>
    public virtual void Remove(file.@this p) { }

    /// <summary>What is at the path: a file with its size (in <paramref name="context"/>'s standard), a folder, or
    /// nothing.</summary>
    public virtual StatInfo Stat(file.@this p, actor.context.@this context)
    {
        if (IsFile(p))
        {
            var info = new System.IO.FileInfo(p.Absolute);
            return new StatInfo(Exists: true, IsFile: true, Size: global::app.type.item.size.@this.Create(info.Length, context),
                Modified: info.LastWriteTimeUtc);
        }
        if (IsFolder(p))
            return new StatInfo(Exists: true, IsFile: false, Modified: new System.IO.DirectoryInfo(p.Absolute).LastWriteTimeUtc);
        return new StatInfo(Exists: false);
    }

    // The folder a file lands in, made when there is none.
    private void Parent(file.@this p)
    {
        var folder = PathHelper.GetDirectoryName(p.Absolute);
        if (!string.IsNullOrEmpty(folder)) Create(new file.@this(folder, p.Type));
    }
}
