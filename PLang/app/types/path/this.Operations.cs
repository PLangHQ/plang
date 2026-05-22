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

    public abstract Task<data.@this> ReadText();
    public abstract Task<data.@this> ReadBytes();
    public abstract Task<data.@this> ExistsAsync();
    public abstract Task<data.@this> List();
    public abstract Task<data.@this> Stat();

    public abstract Task<data.@this> WriteText(string content);
    public abstract Task<data.@this> WriteBytes(byte[] content);
    public abstract Task<data.@this> Append(string content);
    public abstract Task<data.@this> Mkdir();

    public abstract Task<data.@this> Delete();

    // --- Cross-scheme defaults — virtual; subclasses override for fast paths ---

    /// <summary>
    /// Cross-scheme copy default: ReadBytes from this, WriteBytes to destination.
    /// Authorization is performed by the underlying verb impls (each calls Authorize).
    /// Subclasses (e.g. FilePath) override for same-scheme fast paths.
    /// </summary>
    public virtual async Task<data.@this> CopyTo(@this destination)
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
    public virtual async Task<data.@this> MoveTo(@this destination)
    {
        var copy = await CopyTo(destination);
        if (!copy.Success || copy.Type?.ClrType.Exit() == true) return copy;
        return await Delete();
    }
}
