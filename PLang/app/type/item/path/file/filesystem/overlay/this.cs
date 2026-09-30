using app.Utils;
using StatInfo = global::app.type.item.path.@this.StatInfo;

namespace app.type.item.path.file.filesystem.overlay;

/// <summary>
/// A filesystem over another: what is written, made or deleted through it stays in memory, and what it never
/// touched is read from the one under it. A build checks its goals over one — a file a step saves is there for
/// the step that reads it, and nothing reaches the disk. <see cref="Add"/> puts a file there from C#, a mock.
/// It grants nothing: every caller is a path verb that has already passed its gate.
/// </summary>
public sealed class @this : filesystem.@this
{
    private readonly filesystem.@this _under;
    private readonly object _lock = new();
    // files written or added here, by absolute path
    private readonly Dictionary<string, byte[]> _files;
    // folders made here
    private readonly HashSet<string> _folders;
    // what was deleted here: what the one under holds there is hidden; what is put here after shows
    private readonly HashSet<string> _gone;

    public @this(filesystem.@this under)
    {
        _under = under;
        var comparer = StringComparer.FromComparison(global::app.type.item.path.@this.RootComparison);
        _files = new(comparer);
        _folders = new(comparer);
        _gone = new(comparer);
    }

    /// <summary>Puts a file at <paramref name="p"/> — with <paramref name="content"/>, or empty when what it
    /// holds is not known (a file a step will write at run).</summary>
    public void Add(file.@this p, byte[]? content = null)
    {
        lock (_lock) _files[p.Absolute] = content ?? [];
    }

    public override bool IsFile(file.@this p)
    {
        lock (_lock)
        {
            if (_files.ContainsKey(p.Absolute)) return true;
            if (Gone(p.Absolute)) return false;
        }
        return _under.IsFile(p);
    }

    public override bool IsFolder(file.@this p)
    {
        lock (_lock)
        {
            if (Holds(p.Absolute)) return true;
            if (Gone(p.Absolute)) return false;
        }
        return _under.IsFolder(p);
    }

    public override Task<byte[]> Read(file.@this p)
    {
        lock (_lock)
        {
            if (_files.TryGetValue(p.Absolute, out var content)) return Task.FromResult(content);
            if (Gone(p.Absolute)) throw new System.IO.FileNotFoundException($"Not found: {p}", p.Absolute);
        }
        return _under.Read(p);
    }

    public override Task Write(file.@this p, byte[] content)
    {
        lock (_lock) _files[p.Absolute] = content;
        return Task.CompletedTask;
    }

    public override async Task Append(file.@this p, string text)
    {
        byte[] existing = IsFile(p) ? await Read(p) : [];
        lock (_lock) _files[p.Absolute] = [.. existing, .. System.Text.Encoding.UTF8.GetBytes(text)];
    }

    public override void Delete(file.@this p, bool recursive)
    {
        lock (_lock)
        {
            _files.Keys.Where(k => Within(k, p.Absolute)).ToList().ForEach(k => _files.Remove(k));
            _folders.RemoveWhere(k => Within(k, p.Absolute));
            _gone.Add(p.Absolute);
        }
    }

    public override void Create(file.@this p)
    {
        lock (_lock) _folders.Add(p.Absolute);
    }

    public override IEnumerable<file.@this> List(file.@this folder, string pattern, bool recursive)
    {
        var entries = new HashSet<string>(StringComparer.FromComparison(global::app.type.item.path.@this.RootComparison));
        var prefix = folder.Absolute.TrimEnd(PathHelper.DirectorySeparatorChar) + PathHelper.DirectorySeparatorChar;
        lock (_lock)
        {
            // what was put here below the folder, and the folders it lies in
            foreach (var entry in Own().Where(entry => Under(entry, folder.Absolute)))
            {
                var segments = entry[prefix.Length..].Split(PathHelper.DirectorySeparatorChar);
                for (int depth = 1; depth <= (recursive ? segments.Length : 1); depth++)
                    entries.Add(prefix + string.Join(PathHelper.DirectorySeparatorChar, segments[..depth]));
            }
            entries.RemoveWhere(entry => !System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, PathHelper.GetFileName(entry)));
        }
        bool hidden;
        lock (_lock) hidden = Gone(folder.Absolute);
        if (!hidden && _under.IsFolder(folder))
            foreach (var entry in _under.List(folder, pattern, recursive))
                lock (_lock) if (!Gone(entry.Absolute)) entries.Add(entry.Absolute);
        return entries.Select(entry => new file.@this(entry)).ToList();
    }

    public override async Task Move(file.@this from, file.@this to, bool overwrite)
    {
        if (IsFolder(from))
        {
            if (overwrite && IsFolder(to)) Delete(to, recursive: true);
            Create(to);
            foreach (var entry in List(from, "*", recursive: true).Where(IsFile).ToList())
                await Copy(entry, new file.@this(to.Absolute + entry.Absolute[from.Absolute.Length..]), overwrite);
        }
        else await Copy(from, to, overwrite);
        Delete(from, recursive: true);
    }

    public override async Task Copy(file.@this from, file.@this to, bool overwrite)
    {
        if (!overwrite && IsFile(to)) throw new System.IO.IOException($"The file '{to}' already exists.");
        var content = await Read(from);
        lock (_lock) _files[to.Absolute] = content;
    }

    public override StatInfo Stat(file.@this p)
    {
        lock (_lock)
        {
            if (_files.TryGetValue(p.Absolute, out var content))
                return new StatInfo(Exists: true, IsFile: true, Length: content.Length, Modified: DateTime.UtcNow);
            if (Holds(p.Absolute)) return new StatInfo(Exists: true, IsFile: false, Modified: DateTime.UtcNow);
            if (Gone(p.Absolute)) return new StatInfo(Exists: false);
        }
        return _under.Stat(p);
    }

    private IEnumerable<string> Own() => _files.Keys.Concat(_folders);

    // a folder made here, or one what was put here lies in
    private bool Holds(string folder) => _folders.Contains(folder) || Own().Any(entry => Under(entry, folder));

    private bool Gone(string absolute) => _gone.Any(gone => Within(absolute, gone));

    // the path itself, or anything under it
    private bool Within(string entry, string folder)
        => string.Equals(entry, folder, global::app.type.item.path.@this.RootComparison) || Under(entry, folder);

    private bool Under(string entry, string folder)
        => entry.StartsWith(folder.TrimEnd(PathHelper.DirectorySeparatorChar) + PathHelper.DirectorySeparatorChar,
            global::app.type.item.path.@this.RootComparison);
}
