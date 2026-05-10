using App.Utils;
using App.Attributes;

namespace App.FileSystem;

/// <summary>
/// Plain domain class representing a filesystem path.
/// NOT a Data subclass — wrapped in Data&lt;Path&gt; by handlers.
/// Implements IContext for runtime graph access (FileSystem, etc.).
/// </summary>
[PlangType("path",
    Example = "/some/file.json")]
public class Path : modules.IContext
{
    private readonly string _absolutePath;

    // Cached string-derived properties
    private string? _extension;
    private string? _fileName;
    private string? _fileNameWithoutExtension;
    private string? _directory;
    private string? _relative;

    /// <summary>
    /// Lazy FileSystem access — resolved from Context.App.FileSystem.
    /// Context can be supplied via constructor, or set later through the IContext
    /// interface (Data&lt;Path&gt;.Context propagation). Properties that need the
    /// filesystem (Relative, Extension, FileName, etc.) throw if accessed before
    /// Context is wired — by that point in any real flow Context is always
    /// available; only CLI-parse-time and direct test construction skip it.
    /// </summary>
    private IPLangFileSystem Fs => Context?.App?.FileSystem
        ?? throw new InvalidOperationException(
            "Path requires Context with App.FileSystem — wire it before accessing filesystem-dependent properties");

    /// <summary>
    /// Creates a Path. Accepts either an absolute path or a relative one — when
    /// the input is relative AND <paramref name="context"/> is supplied, it's
    /// resolved against the goal's runtime directory the same way the source
    /// generator does. Absolute paths are stored as-is. Context is optional so
    /// boot-time utilities (CLI parser, generators) can still construct Paths
    /// before runtime is up; in that case Context arrives later via IContext.
    /// </summary>
    public Path(string path, Actor.Context.@this? context = null, object? content = null, string? source = null)
    {
        Raw = path;
        Context = context;
        Content = content;
        Source = source;

        _absolutePath = (context != null && IsRelative(path))
            ? ResolveRelative(path, context)
            : path;
    }

    /// <summary>
    /// Context for runtime access. Settable through IContext (Data propagates
    /// it automatically when Path is inside Data&lt;Path&gt;).
    /// JsonIgnore — Context references the runtime graph (App, Culture, parents)
    /// which contains cycles; serializing it blows up trace files.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Actor.Context.@this? Context { get; set; }

    /// <summary>Source generator convention — auto-wraps string parameters.</summary>
    public static Path Resolve(string rawPath, Actor.Context.@this context)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        ArgumentNullException.ThrowIfNull(context);
        return new Path(rawPath, context);
    }

    private static bool IsRelative(string path)
        => !path.StartsWith('/') && !path.StartsWith('\\') && !path.Contains("://");

    /// <summary>
    /// Resolves a relative path against the goal's runtime directory (preferred,
    /// derived from the .pr's on-disk location) or falls back to Goal.Path's
    /// directory for in-memory goals. Validated through the FileSystem before
    /// being stored as the absolute path.
    /// </summary>
    private static string ResolveRelative(string rawPath, Actor.Context.@this context)
    {
        var fs = context.App.FileSystem;
        var resolved = rawPath;

        var goal = context.Goal;
        var runtimeDir = goal?.GetRuntimeDirectory();
        if (!string.IsNullOrEmpty(runtimeDir))
        {
            resolved = fs.Path.Combine(runtimeDir, rawPath);
        }
        else
        {
            var goalPath = goal?.Path;
            if (!string.IsNullOrEmpty(goalPath))
            {
                var goalDir = fs.Path.GetDirectoryName(goalPath);
                if (!string.IsNullOrEmpty(goalDir))
                    resolved = fs.Path.Combine(goalDir, rawPath);
            }
        }

        return fs.ValidatePath(resolved);
    }

    // --- Path properties ---

    public string Raw { get; private init; } = "";
    public string Absolute => _absolutePath;

    public string Relative
    {
        get
        {
            if (_relative != null) return _relative;

            var root = Fs.RootDirectory;
            if (!root.EndsWith(Fs.Path.DirectorySeparatorChar) && !root.EndsWith(Fs.Path.AltDirectorySeparatorChar))
                root += Fs.Path.DirectorySeparatorChar;

            if (_absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                _relative = _absolutePath[root.Length..];
            else if (string.Equals(_absolutePath, Fs.RootDirectory, StringComparison.OrdinalIgnoreCase))
                _relative = ".";
            else
                _relative = _absolutePath;

            return _relative;
        }
    }

    [LlmBuilder] public string Extension => _extension ??= Fs.Path.GetExtension(_absolutePath);
    [LlmBuilder] public string FileName => _fileName ??= Fs.Path.GetFileName(_absolutePath);
    [LlmBuilder] public string FileNameWithoutExtension
        => _fileNameWithoutExtension ??= Fs.Path.GetFileNameWithoutExtension(_absolutePath);
    [LlmBuilder] public string Directory => _directory ??= Fs.Path.GetDirectoryName(_absolutePath) ?? _absolutePath;
    [LlmBuilder] public string MimeType => Context?.App?.Formats?.Mime(Extension) ?? "application/octet-stream";

    [LlmBuilder] public bool IsFile => !string.IsNullOrEmpty(Extension);
    [LlmBuilder] public bool IsDirectory => string.IsNullOrEmpty(Extension);

    /// <summary>
    /// Converts this path to a GoalCall. Derives PrPath from the .goal file path.
    /// </summary>
    public Goals.Goal.GoalCall GoalCall
    {
        get
        {
            var rel = Relative.Replace('\\', '/');
            var dir = Fs.Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "";
            var baseName = Fs.Path.GetFileNameWithoutExtension(rel);
            var prDir = string.IsNullOrEmpty(dir) ? ".build" : $"{dir}/.build";
            var prPath = $"/{prDir}/{baseName.ToLowerInvariant()}.pr";
            return new Goals.Goal.GoalCall { Name = "", PrPath = prPath };
        }
    }

    // --- Live filesystem properties ---

    [LlmBuilder] public bool Exists => Fs.File.Exists(_absolutePath) || Fs.Directory.Exists(_absolutePath);

    [LlmBuilder] public long Size
    {
        get
        {
            var info = Fs.FileInfo.New(_absolutePath);
            return info.Exists ? info.Length : 0;
        }
    }

    // --- Content (file content when set by provider, e.g., after file.read) ---

    public object? Content { get; set; }

    /// <summary>
    /// Reads the file at this path, routed through the registered <c>file.read</c>
    /// provider (<see cref="modules.file.code.IFile"/>). The result is the standard
    /// <see cref="App.Data.@this"/> envelope — <c>.Value</c> is typically a string.
    /// First successful read memoises into <see cref="Content"/>; subsequent calls
    /// return the cached value without re-reading.
    ///
    /// Path owns this. Scripts say <c>await path.GetContent()</c> — they don't
    /// look up <c>IFile</c>, don't construct a <c>file.read</c> action record.
    /// </summary>
    public Task<App.Data.@this> GetContent()
    {
        if (Content != null) return Task.FromResult(App.Data.@this.Ok(Content));

        var ctx = Context ?? throw new InvalidOperationException(
            "Path.GetContent requires Context — wire it before reading");

        var action = new modules.file.Read
        {
            Path    = new App.Data.@this<Path>(value: this),
            Context = ctx,
        };

        var provider = ctx.App.Code.Get<modules.file.code.IFile>().Value
            ?? throw new InvalidOperationException("No file provider registered");

        var result = provider.Read(action);
        if (result.Success && result.Value is not null) Content = result.Value;
        return Task.FromResult(result);
    }

    // --- Copy/move source tracking ---

    public string? Source { get; }

    // --- Display ---

    public override string ToString() => Content?.ToString() ?? Relative;

    public override bool Equals(object? obj) => obj switch
    {
        Path other => string.Equals(_absolutePath, other._absolutePath, StringComparison.OrdinalIgnoreCase),
        string str => string.Equals(_absolutePath, str, StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_absolutePath);
}
