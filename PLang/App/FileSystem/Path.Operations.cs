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
    // --- Reads ---------------------------------------------------------------

    public async Task<Data.@this> ReadText()
    {
        var auth = await Authorize(new Verb { Read = new ReadVerb() });
        if (auth.Type?.ClrType.Exit() == true) return auth;
        if (!auth.Success) return auth;
        var text = await System.IO.File.ReadAllTextAsync(Absolute);
        return Data.@this.Ok(text);
    }

    public async Task<Data.@this> ReadBytes()
    {
        var auth = await Authorize(new Verb { Read = new ReadVerb() });
        if (auth.Type?.ClrType.Exit() == true) return auth;
        if (!auth.Success) return auth;
        var bytes = await System.IO.File.ReadAllBytesAsync(Absolute);
        return Data.@this.Ok(bytes);
    }

    public async Task<Data.@this> ExistsAsync()
    {
        var auth = await Authorize(new Verb { Read = new ReadVerb() });
        if (auth.Type?.ClrType.Exit() == true) return auth;
        if (!auth.Success) return auth;
        var exists = System.IO.File.Exists(Absolute) || System.IO.Directory.Exists(Absolute);
        return Data.@this.Ok(exists);
    }

    public async Task<Data.@this> List()
    {
        var auth = await Authorize(new Verb { Read = new ReadVerb() });
        if (auth.Type?.ClrType.Exit() == true) return auth;
        if (!auth.Success) return auth;
        if (!System.IO.Directory.Exists(Absolute)) return Data.@this.Ok(Array.Empty<string>());
        var entries = System.IO.Directory.EnumerateFileSystemEntries(Absolute).ToArray();
        return Data.@this.Ok(entries);
    }

    public async Task<Data.@this> Stat()
    {
        var auth = await Authorize(new Verb { Read = new ReadVerb(Metadata: true) });
        if (auth.Type?.ClrType.Exit() == true) return auth;
        if (!auth.Success) return auth;
        if (System.IO.File.Exists(Absolute))
        {
            var info = new System.IO.FileInfo(Absolute);
            return Data.@this.Ok(new Dictionary<string, object?>
            {
                ["exists"] = true, ["isFile"] = true,
                ["length"] = info.Length, ["modified"] = info.LastWriteTimeUtc,
            });
        }
        if (System.IO.Directory.Exists(Absolute))
        {
            var info = new System.IO.DirectoryInfo(Absolute);
            return Data.@this.Ok(new Dictionary<string, object?>
            {
                ["exists"] = true, ["isFile"] = false,
                ["modified"] = info.LastWriteTimeUtc,
            });
        }
        return Data.@this.Ok(new Dictionary<string, object?> { ["exists"] = false });
    }

    // --- Writes --------------------------------------------------------------

    public async Task<Data.@this> WriteText(string content)
    {
        var auth = await Authorize(new Verb { Write = new WriteVerb() });
        if (auth.Type?.ClrType.Exit() == true) return auth;
        if (!auth.Success) return auth;
        var dir = System.IO.Path.GetDirectoryName(Absolute);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        await System.IO.File.WriteAllTextAsync(Absolute, content);
        return Data.@this.Ok();
    }

    public async Task<Data.@this> WriteBytes(byte[] content)
    {
        var auth = await Authorize(new Verb { Write = new WriteVerb() });
        if (auth.Type?.ClrType.Exit() == true) return auth;
        if (!auth.Success) return auth;
        var dir = System.IO.Path.GetDirectoryName(Absolute);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        await System.IO.File.WriteAllBytesAsync(Absolute, content);
        return Data.@this.Ok();
    }

    public async Task<Data.@this> Append(string content)
    {
        var auth = await Authorize(new Verb { Write = new WriteVerb() });
        if (auth.Type?.ClrType.Exit() == true) return auth;
        if (!auth.Success) return auth;
        var dir = System.IO.Path.GetDirectoryName(Absolute);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        await System.IO.File.AppendAllTextAsync(Absolute, content);
        return Data.@this.Ok();
    }

    public async Task<Data.@this> Mkdir()
    {
        var auth = await Authorize(new Verb { Write = new WriteVerb() });
        if (auth.Type?.ClrType.Exit() == true) return auth;
        if (!auth.Success) return auth;
        System.IO.Directory.CreateDirectory(Absolute);
        return Data.@this.Ok();
    }

    // --- Destructive ---------------------------------------------------------

    public async Task<Data.@this> Delete()
    {
        var auth = await Authorize(new Verb { Delete = new DeleteVerb() });
        if (auth.Type?.ClrType.Exit() == true) return auth;
        if (!auth.Success) return auth;
        if (System.IO.File.Exists(Absolute)) { System.IO.File.Delete(Absolute); return Data.@this.Ok(); }
        if (System.IO.Directory.Exists(Absolute)) { System.IO.Directory.Delete(Absolute, recursive: true); return Data.@this.Ok(); }
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

        // At least one needs an Ask. Build a bundled prompt covering only the
        // missing paths so the user sees a single question.
        var sb = new StringBuilder();
        var actorName = Context!.Actor!.Name;
        sb.Append(actorName).Append(" wants to:");
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
                if (!sourceOk) StoreGrant(sourceVerb, persist: true);
                if (!destOk)   destination.StoreGrant(destVerb, persist: true);
                return await PerformTransfer(destination, isMove);
            case "y":
                if (!sourceOk) StoreGrant(sourceVerb, persist: false);
                if (!destOk)   destination.StoreGrant(destVerb, persist: false);
                return await PerformTransfer(destination, isMove);
            case "n":
                var denied = !sourceOk
                    ? new global::App.Errors.PermissionDenied(BuildRequest(Context!.Actor!, sourceVerb))
                    : new global::App.Errors.PermissionDenied(BuildRequest(Context!.Actor!, destVerb));
                return Data.@this.FromError(denied);
            default:
                // Re-prompt on garbage. Recurse keeps the bundling.
                return await BundledTransfer(destination, isMove);
        }
    }

    private async Task<Data.@this?> TryAuthorizeWithoutAsk(Verb verb)
    {
        if (IsInRoot()) return Data.@this.Ok();
        var existing = Context?.Actor?.Permission.Find(this, verb);
        return existing != null ? Data.@this.Ok() : null;
    }

    private void StoreGrant(Verb verb, bool persist)
    {
        var permission = BuildRequest(Context!.Actor!, verb);
        var data = new Data.@this<global::App.FileSystem.Permission.@this>("", permission) { Context = Context };
        if (persist) data.EnsureSigned();
        Context!.Actor!.Permission.Add(data);
    }

    private async Task<Data.@this> PerformTransfer(Path destination, bool isMove)
    {
        var destDir = System.IO.Path.GetDirectoryName(destination.Absolute);
        if (!string.IsNullOrEmpty(destDir) && !System.IO.Directory.Exists(destDir))
            System.IO.Directory.CreateDirectory(destDir);
        if (isMove) System.IO.File.Move(Absolute, destination.Absolute, overwrite: true);
        else        System.IO.File.Copy(Absolute, destination.Absolute, overwrite: true);
        return await Task.FromResult(Data.@this.Ok());
    }
}
