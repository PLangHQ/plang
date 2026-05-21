using System.Text;
using App.Types;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using ReadVerb = global::App.FileSystem.Permission.Verb.Read;
using WriteVerb = global::App.FileSystem.Permission.Verb.Write;
using DeleteVerb = global::App.FileSystem.Permission.Verb.Delete;

namespace App.FileSystem;

/// <summary>
/// IPLangFileSystem v2 — Path-in, Data-out FS surface. Each operation calls
/// <see cref="Authorize"/> first; in-root paths auto-grant, out-of-root paths
/// either match an existing grant or prompt via <c>output.ask</c>. Permission
/// miss surfaces as <c>Data&lt;Ask&gt;</c> (Exit-typed) — engine short-circuits
/// the goal; channel decides materialisation (Stream blocks, Message wires).
///
/// The handlers under <c>PLang/App/modules/file/*.cs</c> become thin shells
/// (one-liner each) on top of this surface. The legacy <c>fs.File</c>/
/// <c>fs.Directory</c> calls remain in non-file-action sites (builder,
/// snapshot, settings) — those don't go through the permission gate (they're
/// internal infra). When the spec's "delete the old surface" follow-up
/// lands, those sites move here too.
/// </summary>
public partial class Path
{
    /// <summary>
    /// Stat() result payload. Exists=false → all other fields null.
    /// IsFile=true → file (Length set). IsFile=false → directory.
    /// Nested under Path so callers reach it as <c>Path.StatInfo</c>.
    /// </summary>
    public sealed record StatInfo(bool Exists, bool? IsFile = null, long? Length = null, DateTime? Modified = null);

    /// <summary>
    /// Authorize + Exit-bubble guard. Returns non-null when the caller should
    /// return early (either the gate denied or the result is Exit-typed and
    /// must bubble to the step loop). Returns null on grant — caller proceeds
    /// with the IO.
    /// </summary>
    private async Task<Data.@this?> AuthGate(Verb verb)
    {
        var auth = await Authorize(verb);
        if (auth.Type?.ClrType.Exit() == true) return auth;
        if (!auth.Success) return auth;
        return null;
    }

    private void EnsureParentDir()
    {
        var dir = System.IO.Path.GetDirectoryName(Absolute);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);
    }

    // --- Reads ---------------------------------------------------------------

    public async Task<Data.@this> ReadText()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
        return Data.@this.Ok(await System.IO.File.ReadAllTextAsync(Absolute));
    }

    public async Task<Data.@this> ReadBytes()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
        return Data.@this.Ok(await System.IO.File.ReadAllBytesAsync(Absolute));
    }

    public async Task<Data.@this> ExistsAsync()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
        return Data.@this.Ok(System.IO.File.Exists(Absolute) || System.IO.Directory.Exists(Absolute));
    }

    public async Task<Data.@this> List()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
        if (!System.IO.Directory.Exists(Absolute)) return Data.@this.Ok(Array.Empty<string>());
        return Data.@this.Ok(System.IO.Directory.EnumerateFileSystemEntries(Absolute).ToArray());
    }

    public async Task<Data.@this> Stat()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb(Metadata: true) }) is { } early) return early;
        if (System.IO.File.Exists(Absolute))
        {
            var info = new System.IO.FileInfo(Absolute);
            return Data.@this.Ok(new StatInfo(Exists: true, IsFile: true, Length: info.Length, Modified: info.LastWriteTimeUtc));
        }
        if (System.IO.Directory.Exists(Absolute))
        {
            var info = new System.IO.DirectoryInfo(Absolute);
            return Data.@this.Ok(new StatInfo(Exists: true, IsFile: false, Modified: info.LastWriteTimeUtc));
        }
        return Data.@this.Ok(new StatInfo(Exists: false));
    }

    // --- Writes --------------------------------------------------------------

    public async Task<Data.@this> WriteText(string content)
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return early;
        EnsureParentDir();
        await System.IO.File.WriteAllTextAsync(Absolute, content);
        return Data.@this.Ok();
    }

    public async Task<Data.@this> WriteBytes(byte[] content)
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return early;
        EnsureParentDir();
        await System.IO.File.WriteAllBytesAsync(Absolute, content);
        return Data.@this.Ok();
    }

    public async Task<Data.@this> Append(string content)
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return early;
        EnsureParentDir();
        await System.IO.File.AppendAllTextAsync(Absolute, content);
        return Data.@this.Ok();
    }

    public async Task<Data.@this> Mkdir()
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return early;
        System.IO.Directory.CreateDirectory(Absolute);
        return Data.@this.Ok();
    }

    // --- Destructive ---------------------------------------------------------
    // Delete returns Ok when neither file nor directory exists — idempotent
    // `rm -f` semantics. Caller can pre-check Exists() for strict-mode flows.

    public async Task<Data.@this> Delete()
    {
        if (await AuthGate(new Verb { Delete = new DeleteVerb() }) is { } early) return early;
        if (System.IO.File.Exists(Absolute)) System.IO.File.Delete(Absolute);
        else if (System.IO.Directory.Exists(Absolute)) System.IO.Directory.Delete(Absolute, recursive: true);
        return Data.@this.Ok();
    }

    // --- Multi-path: Move / Copy --------------------------------------------

    public async Task<Data.@this> MoveTo(Path destination) =>
        await BundledTransfer(destination, isMove: true);

    public async Task<Data.@this> CopyTo(Path destination) =>
        await BundledTransfer(destination, isMove: false);

    private async Task<Data.@this> BundledTransfer(Path destination, bool isMove)
    {
        var sourceVerb = new Verb { Read = new ReadVerb() };
        var destVerb   = new Verb { Write = new WriteVerb() };

        // Ask each path its respective verb. If either is missing, bundle.
        var sourceAuth = await TryAuthorizeWithoutAsk(sourceVerb);
        var destAuth   = await destination.TryAuthorizeWithoutAsk(destVerb);

        bool sourceOk = sourceAuth?.Success == true;
        bool destOk   = destAuth?.Success == true;

        if (sourceOk && destOk)
        {
            // Both granted (or in-root) — proceed.
            return await PerformTransfer(destination, isMove);
        }

        // At least one needs an Ask. Loop on garbage input — recursion would
        // grow the async state machine without bound.
        string prefix = "";
        while (true)
        {
            var sb = new StringBuilder();
            sb.Append(prefix);
            sb.Append(Context!.Actor!.Name).Append(" wants to:");
            if (!sourceOk) sb.Append("\n  - read ").Append(Absolute);
            if (!destOk)   sb.Append("\n  - write ").Append(destination.Absolute);
            sb.Append("\n(y/n/a — covers all)");

            var askAction = new modules.output.ask
            {
                Context = Context,
                Question = new Data.@this<string>("", sb.ToString()),
            };
            var askResult = await Context!.App.RunAction(askAction, Context);

            if (askResult.Type?.ClrType.Exit() == true) return askResult;
            if (!askResult.Success) return askResult;

            var answer = askResult.Value?.ToString()?.Trim();
            switch (answer)
            {
                case "a":
                    if (!sourceOk) await StoreGrant(sourceVerb, persist: true);
                    if (!destOk)   await destination.StoreGrant(destVerb, persist: true);
                    return await PerformTransfer(destination, isMove);
                case "y":
                    if (!sourceOk) await StoreGrant(sourceVerb, persist: false);
                    if (!destOk)   await destination.StoreGrant(destVerb, persist: false);
                    return await PerformTransfer(destination, isMove);
                case "n":
                    var denied = !sourceOk
                        ? new global::App.Errors.PermissionDenied(BuildRequest(Context!.Actor!, sourceVerb))
                        : new global::App.Errors.PermissionDenied(BuildRequest(Context!.Actor!, destVerb));
                    return Data.@this.FromError(denied);
                default:
                    prefix = $"Invalid answer '{answer}'. ";
                    continue;
            }
        }
    }

    private async Task<Data.@this?> TryAuthorizeWithoutAsk(Verb verb)
    {
        if (IsInRoot()) return Data.@this.Ok();
        var existing = await Context!.Actor!.Permission.Find(this, verb);
        return existing != null ? Data.@this.Ok() : null;
    }

    private async Task StoreGrant(Verb verb, bool persist)
    {
        var permission = BuildRequest(Context!.Actor!, verb);
        var data = new Data.@this<global::App.FileSystem.Permission.@this>("", permission) { Context = Context };
        if (persist) data.EnsureSigned();
        await Context!.Actor!.Permission.Add(data);
    }

    private Task<Data.@this> PerformTransfer(Path destination, bool isMove)
    {
        var destDir = System.IO.Path.GetDirectoryName(destination.Absolute);
        if (!string.IsNullOrEmpty(destDir) && !System.IO.Directory.Exists(destDir))
            System.IO.Directory.CreateDirectory(destDir);
        if (isMove) System.IO.File.Move(Absolute, destination.Absolute, overwrite: true);
        else        System.IO.File.Copy(Absolute, destination.Absolute, overwrite: true);
        return Task.FromResult(Data.@this.Ok());
    }
}
