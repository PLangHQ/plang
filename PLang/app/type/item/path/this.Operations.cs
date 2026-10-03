using app.type;
using app.type.list;
using Verb = global::app.type.item.permission.Verb;

namespace app.type.item.path;

/// <summary>
/// Abstract verb surface for any <see cref="@this">Path</see> subclass.
/// FilePath/HttpPath/S3Path etc. each implement the per-scheme bodies — calling
/// <see cref="Authorize"/> (the scheme-agnostic Permission gate) internally
/// from each impl. Cross-scheme <see cref="CopyTo"/>/<see cref="MoveTo"/> stay
/// virtual on the base with naive read/write defaults; same-scheme subclasses
/// override for fast paths (FilePath moves through its caller's filesystem, etc.).
/// Every verb takes the caller's context: it checks the caller's permission and
/// its result Data is born with it. The path itself stores none.
/// </summary>
public abstract partial class @this
{
    /// <summary>
    /// Stat() result payload. Exists=false → all other fields null.
    /// IsFile=true → file (Size set). IsFile=false → directory.
    /// Nested under path so callers reach it as <c>path.@this.StatInfo</c>.
    /// </summary>
    public sealed class StatInfo : global::app.type.item.@this, global::app.type.item.ICreate<StatInfo>
    {
        [Out] public bool Exists { get; }
        [Out] public bool? IsFile { get; }
        /// <summary>How big the file is — a size, written in its asker's standard.</summary>
        [Out] public global::app.type.item.size.@this? Size { get; }
        [Out] public DateTime? Modified { get; }
        public StatInfo(bool Exists, bool? IsFile = null, global::app.type.item.size.@this? Size = null, DateTime? Modified = null)
        {
            this.Exists = Exists; this.IsFile = IsFile; this.Size = Size; this.Modified = Modified;
        }
    }

    /// <summary>
    /// Authorize + Exit-bubble guard. Returns non-null when the caller should
    /// return early (either the gate denied or the result is Exit-typed and
    /// must bubble to the step loop). Returns null on grant — caller proceeds
    /// with the IO. Stays on base; reused by every scheme's verb impl.
    /// </summary>
    protected async Task<data.@this?> AuthGate(Verb verb, actor.context.@this context)
    {
        var auth = await Authorize(verb, context);
        if (auth.Exits) return auth;
        if (!auth.Success) return auth;
        return null;
    }

    // --- Abstract verb surface ---
    //
    // The option-bearing verbs (Delete/List/CopyTo/MoveTo/Save) live here, on
    // the base — so a file action handler calls them through the abstract
    // `path` reference and never downcasts to a concrete scheme. Filesystem-only
    // options (recursive, subfolder, overwrite, pattern) are honoured by
    // FilePath and documented as no-ops by non-FS schemes — the no-op lives
    // inside the scheme, not as a branch the handler picks.

    // Read is polymorphic (bare Data): it lands a file, url or directory reference. The
    // other verbs have a single fixed shape — typed.
    /// <summary>What reading this location lands: a reference to what is there, with nothing read — its
    /// content is the reference's own value, read at first touch. <paramref name="template"/> marks the
    /// content that kind of template (its variables are filled at use) — a fact of the reference, whatever its
    /// scheme; none, it is the content as written.</summary>
    public abstract Task<data.@this> Read(actor.context.@this context, global::app.type.item.template.kind.@this? template = null);

    /// <summary>The places this path names, the first the one it means: a path is the one place it is; a file path
    /// under <c>/system/</c> is the app's own and then the os's.</summary>
    public virtual IReadOnlyList<@this> Place(actor.context.@this context) => [this];

    /// <summary>Read's build face: the type <see cref="Read"/> lands, with no content read — the build knows
    /// what a later step captures.</summary>
    public abstract Task<data.@this> Expect(actor.context.@this context);

    /// <summary>Why this location isn't there at build, for a warning — null when the build doesn't ask (a url
    /// is not fetched at build) or it is there.</summary>
    public virtual Task<global::app.error.Error?> Absence(actor.context.@this context) => Task.FromResult<global::app.error.Error?>(null);

    /// <summary>Build's face of a write: this location is in its caller's files, for the steps after it. A copy or
    /// a move names its <paramref name="source"/> — into a folder, the file lands under it. Only a file location
    /// has a place in a filesystem; any other does nothing.</summary>
    public virtual void Add(actor.context.@this context, @this? source = null) { }

    /// <summary>Build's face of a delete: this location is gone from its caller's files, for the steps after it.
    /// Only a file location has a place in a filesystem; any other does nothing.</summary>
    public virtual void Remove(actor.context.@this context) { }

    // The type a reference of this location is: the reference type named, of this location's content kind.
    protected global::app.type.@this Reference(string type, actor.context.@this context)
        => context.App.type.list[new global::app.type.@this(type, Kind(context).kind is { IsEmpty: false } k ? k.Name : null), context];

    /// <summary>The raw bytes at this location, through the gate — asked in C# by whatever needs the bytes
    /// themselves (a reference sampling its content, a request body, an attachment). A program reads a location
    /// through <see cref="Read"/>, which lands a value; this is the bytes, not a value.</summary>
    internal abstract Task<data.@this<global::app.type.item.binary.@this>> Bytes(actor.context.@this context);
    /// <summary>Whether something is at this location, as its asker may see: through the gate — a refusal is the
    /// answer as it is; nothing there is false.</summary>
    [LlmBuilder] public abstract Task<data.@this<global::app.type.item.@bool.@this>> Exists(actor.context.@this context);
    public abstract Task<data.@this<StatInfo>> Stat(actor.context.@this context);

    // Writes return the path itself wrapped — caller can chain or read .Exists.
    public abstract Task<data.@this<@this>> WriteText(string content, actor.context.@this context);
    public abstract Task<data.@this<@this>> WriteBytes(byte[] content, actor.context.@this context);

    /// <summary>Writes <paramref name="content"/> here as it is read, never held whole — gated as a write. Only a file
    /// location has a place to write a stream; any other refuses.</summary>
    public virtual Task<data.@this<@this>> Write(System.IO.Stream content, actor.context.@this context)
        => Task.FromResult(context.Error<@this>(new global::app.error.Error(
            $"{Raw} has no place to write a stream: only a file does", "NotSupported", 400)));
    public abstract Task<data.@this<@this>> Append(string content, actor.context.@this context);
    public abstract Task<data.@this<@this>> Mkdir(actor.context.@this context);

    /// <summary>
    /// Loads a .NET assembly from this path. Gated by <c>Verb { Execute }</c>
    /// — distinct from Read (Unix r/w/x model: reading a DLL is not permission
    /// to load it). FilePath implements; non-filesystem schemes return Fail.
    /// </summary>
    // Assembly is a CLR runtime artifact, not a PLang value — but it rides in a BARE
    // Data (no generic T → satisfies `where T : item`) so the AuthGate ask/exit bubble
    // (a Data Type signal) still propagates. .Value holds the Assembly.
    public virtual Task<data.@this> LoadAssemblyAsync(actor.context.@this context) =>
        Task.FromResult(context.Error(
            new error.ServiceError($"Scheme '{Scheme}' does not support assembly loading.", "NotSupported", 400)));

    /// <summary>Delete what is at this location — a folder with what it holds when <paramref name="recursive"/>
    /// (non-FS schemes ignore it). Nothing there is NotFound (404).</summary>
    public abstract Task<data.@this<@this>> Delete(global::app.type.item.@bool.@this recursive, actor.context.@this context);

    /// <summary>List entries with a glob pattern — the files, or the folders (<paramref name="entry"/>). Non-FS schemes
    /// ignore the options.</summary>
    public abstract Task<data.@this<global::app.type.item.list.@this<@this>>> List(global::app.type.item.text.@this pattern, global::app.type.item.@bool.@this recursive, actor.context.@this context, Entry entry = Entry.file);

    /// <summary>Write <paramref name="value"/> to this path; returns the Path wrapped in Data.</summary>
    public abstract Task<data.@this<@this>> Save(data.@this? value, actor.context.@this context);

    /// <summary>Convenience — same defaults the file actions carried.</summary>
    public Task<data.@this<@this>> Delete(actor.context.@this context) => Delete(recursive: false, context);

    /// <summary>Convenience — all entries, shallow.</summary>
    public Task<data.@this<global::app.type.item.list.@this<@this>>> List(actor.context.@this context) => List(pattern: "*", recursive: false, context);

    // --- Cross-scheme defaults — virtual; subclasses override for fast paths ---

    /// <summary>
    /// Cross-scheme copy default: the bytes from this, WriteBytes to destination.
    /// <paramref name="overwrite"/> / <paramref name="subfolder"/> are
    /// filesystem-only — a byte-stream copy has no folder tree and no in-place
    /// target, so they are no-ops here. Authorization is performed by the
    /// underlying verb impls. Subclasses (e.g. FilePath) override for
    /// same-scheme fast paths that honour the options.
    /// </summary>
    public virtual async Task<data.@this<@this>> CopyTo(@this destination, global::app.type.item.@bool.@this overwrite, global::app.type.item.@bool.@this subfolder, actor.context.@this context)
    {
        var read = await Bytes(context);
        if (!read.Success || read.Exits) return data.@this<@this>.From(read);
        byte[]? copyBytes = (await read.Value())?.Value;
        if (copyBytes == null)
            return context.Error<@this>(new error.Error("CopyTo: the source's bytes did not come back as bytes.", "CopyToReadShape", 500));
        return await destination.WriteBytes(copyBytes, context);
    }

    /// <summary>
    /// Cross-scheme move default: CopyTo destination, then Delete source.
    /// Subclasses (e.g. FilePath same-scheme) override for atomic move semantics.
    /// </summary>
    public virtual async Task<data.@this<@this>> MoveTo(@this destination, global::app.type.item.@bool.@this overwrite, actor.context.@this context)
    {
        var copy = await CopyTo(destination, overwrite, subfolder: true, context);
        if (!copy.Success || copy.Exits) return copy;
        return await Delete(context);
    }

    // --- Boolean resolution (IBooleanResolvable) ---

    /// <summary>
    /// Answers "is this path truthy" — for a path that means "does it exist".
    /// Routed through here by <c>Data.ToBooleanAsync()</c> (which passes its own context)
    /// so a comparison like <c>if %path% exists</c> asks the path itself, as the asker.
    /// FilePath probes the filesystem; HttpPath issues an HTTP HEAD.
    /// </summary>
    public abstract override Task<bool> AsBooleanAsync(actor.context.@this context);
}
