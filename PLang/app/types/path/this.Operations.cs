using app.types;
using Verb = global::app.types.path.permission.verb.@this;

namespace app.types.path;

/// <summary>
/// Abstract verb surface for any <see cref="@this">Path</see> subclass.
/// FilePath/HttpPath/S3Path etc. each implement the per-scheme bodies — calling
/// <see cref="Authorize"/> (the scheme-agnostic Permission gate) internally
/// from each impl. Cross-scheme <see cref="CopyTo"/>/<see cref="MoveTo"/> stay
/// virtual on the base with naive read/write defaults; same-scheme subclasses
/// override for fast paths (FilePath uses <c>System.IO.File.Move</c>, etc.).
/// </summary>
public abstract partial class @this
{
    /// <summary>
    /// Stat() result payload. Exists=false → all other fields null.
    /// IsFile=true → file (Length set). IsFile=false → directory.
    /// Nested under path so callers reach it as <c>path.@this.StatInfo</c>.
    /// </summary>
    public sealed record StatInfo(bool Exists, bool? IsFile = null, long? Length = null, DateTime? Modified = null);

    /// <summary>
    /// Authorize + Exit-bubble guard. Returns non-null when the caller should
    /// return early (either the gate denied or the result is Exit-typed and
    /// must bubble to the step loop). Returns null on grant — caller proceeds
    /// with the IO. Stays on base; reused by every scheme's verb impl.
    /// </summary>
    protected async Task<data.@this?> AuthGate(Verb verb)
    {
        var auth = await Authorize(verb);
        if (auth.Type?.ClrType.Exit() == true) return auth;
        if (!auth.Success) return auth;
        return null;
    }

    // --- Abstract verb surface ---
    //
    // The option-bearing verbs (Delete/List/CopyTo/MoveTo/Save) live here, on
    // the base — so a file action handler calls them through the abstract
    // `path` reference and never downcasts to a concrete scheme. Filesystem-only
    // options (recursive, includeSubfolders, overwrite, pattern) are honoured by
    // FilePath and documented as no-ops by non-FS schemes — the no-op lives
    // inside the scheme, not as a branch the handler picks. (codeanalyzer v1 F1)

    public abstract Task<data.@this> ReadText();
    public abstract Task<data.@this> ReadBytes();
    public abstract Task<data.@this> ExistsAsync();
    public abstract Task<data.@this> Stat();

    public abstract Task<data.@this> WriteText(string content);
    public abstract Task<data.@this> WriteBytes(byte[] content);
    public abstract Task<data.@this> Append(string content);
    public abstract Task<data.@this> Mkdir();

    /// <summary>Delete with file-action options. Non-FS schemes ignore both.</summary>
    public abstract Task<data.@this> Delete(bool recursive, bool ignoreIfNotFound);

    /// <summary>List entries with a glob pattern. Non-FS schemes ignore both options.</summary>
    public abstract Task<data.@this> List(string pattern, bool recursive);

    /// <summary>Write <paramref name="value"/> to this path; returns the Path wrapped in Data.</summary>
    public abstract Task<data.@this> Save(data.@this? value);

    /// <summary>Parameterless convenience — same defaults the file actions carried.</summary>
    public Task<data.@this> Delete() => Delete(recursive: false, ignoreIfNotFound: false);

    /// <summary>Parameterless convenience — all entries, shallow.</summary>
    public Task<data.@this> List() => List(pattern: "*", recursive: false);

    // --- Cross-scheme defaults — virtual; subclasses override for fast paths ---

    /// <summary>
    /// Cross-scheme copy default: ReadBytes from this, WriteBytes to destination.
    /// <paramref name="overwrite"/> / <paramref name="includeSubfolders"/> are
    /// filesystem-only — a byte-stream copy has no folder tree and no in-place
    /// target, so they are no-ops here. Authorization is performed by the
    /// underlying verb impls. Subclasses (e.g. FilePath) override for
    /// same-scheme fast paths that honour the options.
    /// </summary>
    public virtual async Task<data.@this> CopyTo(@this destination, bool overwrite, bool includeSubfolders)
    {
        var read = await ReadBytes();
        if (!read.Success || read.Type?.ClrType.Exit() == true) return read;
        if (read.Value is not byte[] bytes)
            return data.@this.FromError(new errors.Error("CopyTo: source ReadBytes did not return bytes.", "CopyToReadShape", 500));
        return await destination.WriteBytes(bytes);
    }

    /// <summary>
    /// Cross-scheme move default: CopyTo destination, then Delete source.
    /// Subclasses (e.g. FilePath same-scheme) override for atomic move semantics.
    /// </summary>
    public virtual async Task<data.@this> MoveTo(@this destination, bool overwrite)
    {
        var copy = await CopyTo(destination, overwrite, includeSubfolders: true);
        if (!copy.Success || copy.Type?.ClrType.Exit() == true) return copy;
        return await Delete();
    }

    // --- Boolean resolution (IBooleanResolvable) ---

    /// <summary>
    /// Answers "is this path truthy" — for a path that means "does it exist".
    /// Routed through here by <c>Data.ToBooleanAsync()</c> so a comparison like
    /// <c>if %path% exists</c> asks the path itself. FilePath probes the
    /// filesystem; HttpPath issues an HTTP HEAD. (codeanalyzer v1 F3)
    /// </summary>
    public abstract Task<bool> AsBooleanAsync();
}
