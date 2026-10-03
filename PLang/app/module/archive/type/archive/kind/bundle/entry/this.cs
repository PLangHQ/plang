namespace app.module.archive.type.archive.kind.bundle.entry;

/// <summary>
/// One thing a bundle holds — a file with its content, a folder, a link, a hard link to another of its files, or
/// something else (a device, a fifo) that is never made. Its name is from the bundle's root: a leading <c>./</c> or
/// <c>/</c> goes, a dot that starts a name stays (<c>.bashrc</c>, <c>.wh.x</c>).
/// </summary>
public sealed class @this
{
    /// <summary>The name from the bundle's root, <c>/</c>-separated; empty for the root itself.</summary>
    public string Name { get; }

    /// <summary>What it is.</summary>
    public Kind Kind { get; }

    /// <summary>What a link leads to, as written; for a hard link, the name of the file it is, from the bundle's
    /// root.</summary>
    public string? Target { get; }

    /// <summary>Who may read, write and run it, as the bundle says.</summary>
    public System.IO.UnixFileMode Mode { get; }

    /// <summary>A file's content, read once, in order.</summary>
    public System.IO.Stream? Content { get; }

    public @this(string name, Kind kind, System.IO.UnixFileMode mode, string? target = null, System.IO.Stream? content = null)
    {
        Name = Root(name);
        Kind = kind;
        Mode = mode;
        Target = kind == Kind.HardLink && target != null ? Root(target) : target;
        Content = content;
    }

    /// <summary>The folder it is in, from the bundle's root; empty at the root.</summary>
    public string Folder => Name.LastIndexOf('/') is var slash and >= 0 ? Name[..slash] : "";

    /// <summary>Its own name, the last part.</summary>
    public string Leaf => Name.LastIndexOf('/') is var slash and >= 0 ? Name[(slash + 1)..] : Name;

    // a name from the bundle's root: \ is /, leading ./ and / go, a trailing / goes
    private string Root(string name)
    {
        name = name.Replace('\\', '/');
        while (true)
        {
            if (name.StartsWith("./", System.StringComparison.Ordinal)) name = name[2..];
            else if (name.StartsWith('/')) name = name[1..];
            else break;
        }
        name = name.TrimEnd('/');
        return name == "." ? "" : name;
    }
}

/// <summary>What a bundle entry is.</summary>
public enum Kind { File, Folder, Link, HardLink, Other }
