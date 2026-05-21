using App.Types;
using App.Variables;
using App.modules.file.code;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using ReadVerb = global::App.FileSystem.Permission.Verb.Read;
using WriteVerb = global::App.FileSystem.Permission.Verb.Write;

namespace App.modules.file;

[System.ComponentModel.Description("Move or rename a file from Source to Destination, optionally overwriting the target")]
[Action("move", Cacheable = false)]
public partial class Move : IContext
{
    public partial Data.@this<FileSystem.Path> Source { get; init; }
    public partial Data.@this<FileSystem.Path> Destination { get; init; }

    [Default(false)]
    public partial Data.@this<bool> Overwrite { get; init; }

    [Code]
    public partial IFile Files { get; }

    /// <summary>
    /// Authorize source (Read) then destination (Write) in sequence. v1
    /// degradation: two prompts on a fresh out-of-root pair. Bundled-consent
    /// UX lives in the C# Path.MoveTo surface; the action-handler surface
    /// inherits it as a follow-up.
    /// </summary>
    public async Task<Data.@this> Run()
    {
        var srcAuth = await Source.Value!.Authorize(new Verb { Read = new ReadVerb() });
        if (srcAuth.Type?.ClrType.Exit() == true || !srcAuth.Success) return srcAuth;
        var dstAuth = await Destination.Value!.Authorize(new Verb { Write = new WriteVerb() });
        if (dstAuth.Type?.ClrType.Exit() == true || !dstAuth.Success) return dstAuth;
        return Files.Move(this);
    }
}
