using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using app.Utils;

namespace app.type.path.file;

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
            {
                var resolved = PathHelper.GetFullPath(PathHelper.Join(rootAbsolutePath, path));

                // /system/ paths fall back to <os>/system/ when absent under the root.
                var sysPrefix = PathHelper.DirectorySeparatorChar + "system" + PathHelper.DirectorySeparatorChar;
                if (path.AdjustPathToOs().StartsWith(sysPrefix, StringComparison.OrdinalIgnoreCase)
                    && !System.IO.File.Exists(resolved) && !System.IO.Directory.Exists(resolved))
                {
                    var afterPrefix = path.AdjustPathToOs().Substring(sysPrefix.Length);
                    var osResolved = PathHelper.GetFullPath(PathHelper.Join(osAbsolutePath, "system", afterPrefix));
                    if (System.IO.File.Exists(osResolved) || System.IO.Directory.Exists(osResolved))
                        resolved = osResolved;
                }
                path = resolved;
            }
        }
        else
        {
            path = PathHelper.GetFullPath(PathHelper.Join(rootAbsolutePath, path));
        }

        // Out-of-rootAbsolutePath paths are returned as-is — Authorize is the gate, not this method.
        if (!path.StartsWith(rootAbsolutePath, global::app.type.path.@this.RootComparison))
            return path;

        // <root>/system/ → <os>/system/ fallback for non-existent paths.
        var rootSystemDir = rootAbsolutePath + PathHelper.DirectorySeparatorChar + "system" + PathHelper.DirectorySeparatorChar;
        if (path.StartsWith(rootSystemDir, StringComparison.OrdinalIgnoreCase)
            && !System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
        {
            var afterSystem = path.Substring(rootSystemDir.Length);
            var osFallback = PathHelper.GetFullPath(PathHelper.Join(osAbsolutePath, "system", afterSystem));
            if (System.IO.File.Exists(osFallback) || System.IO.Directory.Exists(osFallback))
                path = osFallback;
        }

        return path;
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
