using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using app.Utils;

namespace app.type.item.path.file;

/// <summary>
/// Path-string normalization — relocated from the deleted
/// <c>PLangFileSystem.ValidatePath</c>. This is <b>not</b> a security gate:
/// out-of-root access is gated by <see cref="@this.Authorize"/> /
/// <c>Actor.Permission</c> (the filesystem-permission model). This method
/// only resolves a raw path string to an absolute OS path:
/// <list type="bullet">
///   <item>OS-rooted paths (<c>//tmp/x</c>, <c>C:\…</c>) pass through — the
///   <c>//</c> prefix is preserved for idempotency under repeat calls.</item>
///   <item>PLang-rooted paths (single leading <c>/</c>) anchor to the App
///   root, with a <c>/system/</c> → os-folder fallback.</item>
///   <item>Bare relative paths anchor to the App root.</item>
/// </list>
/// <see cref="Resolve"/> calls this after applying goal-relative resolution.
/// Bootstrap callers with no Goal in scope (App.Load/Save, builder) call it
/// directly.
/// </summary>
public sealed partial class @this
{
    /// <summary>
    /// Normalizes <paramref name="path"/> to an absolute OS path, anchored to
    /// the App root. See the type doc for the rules.
    /// </summary>
    public static string ValidatePath(string? path, global::app.@this app)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path cannot be empty", nameof(path));

        var rootAbsolutePath = PathHelper.GetFullPath(app.AbsolutePath).AdjustPathToOs()
            .TrimEnd(PathHelper.DirectorySeparatorChar);
        var osAbsolutePath = app.OsAbsolutePath;

        if (IsOsRooted(path))
        {
            // Leave the // prefix intact for idempotency — Authorize gates these
            // out-of-root accesses; System.IO normalises // → / at the IO boundary.
        }
        else if (IsPlangRooted(path))
        {
            if (!path.StartsWith(rootAbsolutePath) && !path.StartsWith(osAbsolutePath))
                path = PathHelper.GetFullPath(PathHelper.Join(rootAbsolutePath, path));
        }
        else
        {
            path = PathHelper.GetFullPath(PathHelper.Join(rootAbsolutePath, path));
        }

        // Out-of-rootAbsolutePath paths are returned as-is — Authorize is the gate, not this method.
        if (!path.StartsWith(rootAbsolutePath, global::app.type.item.path.@this.RootComparison))
            return path;

        // of the places it names, the first that is there; else the first whose folder is there (a new file lands
        // beside its siblings); else where it was written
        var places = Places(path, app);
        return places.FirstOrDefault(Present) ?? places.FirstOrDefault(Housed) ?? path;

        // Where a /system/ path is found is decided on the app's disk: the runtime's own files are never a build's.
        bool Present(string absolute) => new @this(absolute) is var at && (app.FileSystem.IsFile(at) || app.FileSystem.IsFolder(at));
        bool Housed(string absolute) => PathHelper.GetDirectoryName(absolute) is { } folder && app.FileSystem.IsFolder(new @this(folder));
    }

    /// <summary>The places this path names, the app's own first: a <c>/system/</c> path is the app's own
    /// (<c>&lt;root&gt;/system/…</c>) and then the os's (<c>&lt;os&gt;/system/…</c>), whichever of them it resolved to;
    /// any other path is the one place it is. What is there is the caller's to ask.</summary>
    public override IReadOnlyList<global::app.type.item.path.@this> Place(actor.context.@this context)
        => Places(Absolute, context.App).Select(place => (global::app.type.item.path.@this)new @this(place, context)).ToList();

    // The /system/ overlay, stated once: an absolute under the app's /system/ or the os's names both, the app's first.
    private static IReadOnlyList<string> Places(string absolute, global::app.@this app)
    {
        var separator = PathHelper.DirectorySeparatorChar;
        var own = PathHelper.GetFullPath(app.AbsolutePath).AdjustPathToOs().TrimEnd(separator) + separator + "system" + separator;
        var os = PathHelper.GetFullPath(app.OsAbsolutePath).AdjustPathToOs().TrimEnd(separator) + separator + "system" + separator;
        if (absolute.StartsWith(own, StringComparison.OrdinalIgnoreCase))
            return [absolute, PathHelper.GetFullPath(os + absolute[own.Length..])];
        if (absolute.StartsWith(os, StringComparison.OrdinalIgnoreCase))
            return [PathHelper.GetFullPath(own + absolute[os.Length..]), absolute];
        return [absolute];
    }

    /// <summary>True for an OS-absolute path — <c>//x</c> on Unix, <c>C:\</c> on Windows.</summary>
    private static bool IsOsRooted(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Regex.IsMatch(path, "^[A-Z]{1}:", RegexOptions.IgnoreCase);
        return path.StartsWith("//");
    }

    /// <summary>True for a PLang-rooted path — a single leading separator.</summary>
    private static bool IsPlangRooted(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path cannot be empty", nameof(path));
        return path.AdjustPathToOs().StartsWith(PathHelper.DirectorySeparatorChar);
    }
}
