using System.Text;
using app.types;
using app.errors;
using Verb = global::app.types.path.permission.verb.@this;
using ReadVerb = global::app.types.path.permission.verb.Read;
using WriteVerb = global::app.types.path.permission.verb.Write;
using DeleteVerb = global::app.types.path.permission.verb.Delete;

namespace app.types.path.file;

/// <summary>
/// FilePath verb implementations — relocated from the (now abstract) base.
/// Every method passes through <see cref="@this.AuthGate"/> (defined on the base)
/// before touching <c>System.IO</c>. Same-scheme MoveTo/CopyTo override the
/// base's naive default with <c>System.IO.File.Move</c>/<c>Copy</c> and the
/// bundled-consent prompt for fresh out-of-root pairs.
/// </summary>
public sealed partial class @this
{
    private void EnsureParentDir()
    {
        var dir = System.IO.Path.GetDirectoryName(Absolute);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);
    }

    // --- Reads ---------------------------------------------------------------

    public override async Task<data.@this> ReadText()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
        return data.@this.Ok(await System.IO.File.ReadAllTextAsync(Absolute));
    }

    public override async Task<data.@this> ReadBytes()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
        return data.@this.Ok(await System.IO.File.ReadAllBytesAsync(Absolute));
    }

    public override async Task<data.@this> ExistsAsync()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
        return data.@this.Ok(System.IO.File.Exists(Absolute) || System.IO.Directory.Exists(Absolute));
    }

    public override async Task<data.@this> List()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
        if (!System.IO.Directory.Exists(Absolute)) return data.@this.Ok(Array.Empty<string>());
        return data.@this.Ok(System.IO.Directory.EnumerateFileSystemEntries(Absolute).ToArray());
    }

    public override async Task<data.@this> Stat()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb(Metadata: true) }) is { } early) return early;
        if (System.IO.File.Exists(Absolute))
        {
            var info = new System.IO.FileInfo(Absolute);
            return data.@this.Ok(new StatInfo(Exists: true, IsFile: true, Length: info.Length, Modified: info.LastWriteTimeUtc));
        }
        if (System.IO.Directory.Exists(Absolute))
        {
            var info = new System.IO.DirectoryInfo(Absolute);
            return data.@this.Ok(new StatInfo(Exists: true, IsFile: false, Modified: info.LastWriteTimeUtc));
        }
        return data.@this.Ok(new StatInfo(Exists: false));
    }

    // --- Writes --------------------------------------------------------------

    public override async Task<data.@this> WriteText(string content)
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return early;
        EnsureParentDir();
        await System.IO.File.WriteAllTextAsync(Absolute, content);
        return data.@this.Ok();
    }

    public override async Task<data.@this> WriteBytes(byte[] content)
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return early;
        EnsureParentDir();
        await System.IO.File.WriteAllBytesAsync(Absolute, content);
        return data.@this.Ok();
    }

    public override async Task<data.@this> Append(string content)
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return early;
        EnsureParentDir();
        await System.IO.File.AppendAllTextAsync(Absolute, content);
        return data.@this.Ok();
    }

    public override async Task<data.@this> Mkdir()
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return early;
        System.IO.Directory.CreateDirectory(Absolute);
        return data.@this.Ok();
    }

    // --- Destructive ---------------------------------------------------------

    public override async Task<data.@this> Delete()
    {
        if (await AuthGate(new Verb { Delete = new DeleteVerb() }) is { } early) return early;
        if (System.IO.File.Exists(Absolute)) System.IO.File.Delete(Absolute);
        else if (System.IO.Directory.Exists(Absolute)) System.IO.Directory.Delete(Absolute, recursive: true);
        return data.@this.Ok();
    }

    // --- Same-scheme fast paths for Move/Copy --------------------------------

    public override async Task<data.@this> MoveTo(global::app.types.path.@this destination)
    {
        if (destination is @this fileDest)
            return await BundledTransfer(fileDest, isMove: true);
        return await base.MoveTo(destination);  // cross-scheme default
    }

    public override async Task<data.@this> CopyTo(global::app.types.path.@this destination)
    {
        if (destination is @this fileDest)
            return await BundledTransfer(fileDest, isMove: false);
        return await base.CopyTo(destination);  // cross-scheme default
    }

    private async Task<data.@this> BundledTransfer(@this destination, bool isMove)
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
            return await PerformTransfer(destination, isMove);
        }

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
                Question = new data.@this<string>("", sb.ToString()),
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
                        ? new global::app.errors.PermissionDenied(BuildRequest(Context!.Actor!, sourceVerb))
                        : new global::app.errors.PermissionDenied(BuildRequest(Context!.Actor!, destVerb));
                    return data.@this.FromError(denied);
                default:
                    prefix = $"Invalid answer '{answer}'. ";
                    continue;
            }
        }
    }

    private async Task<data.@this?> TryAuthorizeWithoutAsk(Verb verb)
    {
        if (IsInRoot()) return data.@this.Ok();
        var existing = await Context!.Actor!.Permission.Find(this, verb);
        return existing != null ? data.@this.Ok() : null;
    }

    private async Task StoreGrant(Verb verb, bool persist)
    {
        var permission = BuildRequest(Context!.Actor!, verb);
        var d = new data.@this<global::app.types.path.permission.@this>("", permission) { Context = Context };
        if (persist) d.EnsureSigned();
        await Context!.Actor!.Permission.Add(d);
    }

    private Task<data.@this> PerformTransfer(@this destination, bool isMove)
    {
        var destDir = System.IO.Path.GetDirectoryName(destination.Absolute);
        if (!string.IsNullOrEmpty(destDir) && !System.IO.Directory.Exists(destDir))
            System.IO.Directory.CreateDirectory(destDir);
        if (isMove) System.IO.File.Move(Absolute, destination.Absolute, overwrite: true);
        else        System.IO.File.Copy(Absolute, destination.Absolute, overwrite: true);
        return Task.FromResult(data.@this.Ok());
    }
}
