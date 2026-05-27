namespace app.Utils;

/// <summary>
/// Pure path-string-math forwarders for <c>System.IO.Path</c>. The single
/// allowed bridge to <c>System.IO.Path</c> in the PLang runtime — everywhere
/// else PLNG002 bans direct <c>System.IO.*</c> reaches.
///
/// <para><b>Contract: no IO, ever.</b> Members here are name math on strings.
/// <c>GetFullPath</c> resolves <c>..</c>/<c>.</c> segments against the current
/// working directory or an explicit base; it does not touch the filesystem.
/// <c>Path.GetTempPath</c>/<c>GetTempFileName</c> do touch environment/disk
/// and MUST NOT be added here — temp-file allocation is an action and lives
/// on the gated <c>path.@this</c> verb surface.</para>
///
/// <para>Anything that touches the filesystem (<c>File.*</c>, <c>Directory.*</c>,
/// <c>FileInfo</c>, <c>FileStream</c>, …) belongs on <c>app.types.path.@this</c>
/// verbs (gated by <c>AuthGate</c>) — not here.</para>
/// </summary>
internal static class PathHelper
{
    // Separator constants — pure data.
    public static char DirectorySeparatorChar => System.IO.Path.DirectorySeparatorChar;
    public static char AltDirectorySeparatorChar => System.IO.Path.AltDirectorySeparatorChar;
    public static char PathSeparator => System.IO.Path.PathSeparator;
    public static char VolumeSeparatorChar => System.IO.Path.VolumeSeparatorChar;

    // Combine / Join — pure name math, no canonicalization, no IO.
    public static string Combine(string a, string b) => System.IO.Path.Combine(a, b);
    public static string Combine(string a, string b, string c) => System.IO.Path.Combine(a, b, c);
    public static string Combine(params string[] parts) => System.IO.Path.Combine(parts);
    public static string Join(string a, string b) => System.IO.Path.Join(a, b);
    public static string Join(string a, string b, string c) => System.IO.Path.Join(a, b, c);

    // Name extraction.
    public static string? GetDirectoryName(string path) => System.IO.Path.GetDirectoryName(path);
    public static string GetFileName(string path) => System.IO.Path.GetFileName(path);
    public static string GetFileNameWithoutExtension(string path) => System.IO.Path.GetFileNameWithoutExtension(path);
    /// <summary>
    /// File extension without the leading dot ("csv", "json") — empty string
    /// when the path has no extension. Diverges from System.IO.Path.GetExtension
    /// (which returns ".csv") because every caller trimmed the dot anyway and
    /// Formats.Mime normalises it back on if needed.
    /// </summary>
    public static string GetExtension(string path)
    {
        var raw = System.IO.Path.GetExtension(path);
        return string.IsNullOrEmpty(raw) ? string.Empty : raw.TrimStart('.');
    }
    public static string ChangeExtension(string path, string? extension) => System.IO.Path.ChangeExtension(path, extension);

    // Pure predicates — name math, no IO.
    public static bool IsPathRooted(string path) => System.IO.Path.IsPathRooted(path);
    public static bool IsPathFullyQualified(string path) => System.IO.Path.IsPathFullyQualified(path);

    /// <summary>
    /// Resolves <c>..</c> and <c>.</c> segments to a canonical absolute path.
    /// Pure string operation — does not stat or read the filesystem.
    /// </summary>
    public static string GetFullPath(string path) => System.IO.Path.GetFullPath(path);
    public static string GetFullPath(string path, string basePath) => System.IO.Path.GetFullPath(path, basePath);
}
