using app.variables;
using app.modules.file.code;
using app.types;
using Verb = global::app.filesystem.permission.verb.@this;
using ReadVerb = global::app.filesystem.permission.verb.Read;
using WriteVerb = global::app.filesystem.permission.verb.Write;

namespace app.modules.file;

[System.ComponentModel.Description("Move or rename a file from Source to Destination, optionally overwriting the target")]
[Action("move", Cacheable = false)]
public partial class Move : IContext
{
    public partial data.@this<filesystem.path> Source { get; init; }
    public partial data.@this<filesystem.path> Destination { get; init; }

    [Default(false)]
    public partial data.@this<bool> Overwrite { get; init; }

    [Code]
    public partial IFile Files { get; }

    /// <summary>
    /// Authorize source (Read) then destination (Write) in sequence. v1
    /// degradation: two prompts on a fresh out-of-root pair. Bundled-consent
    /// UX lives in the C# path.MoveTo surface; the action-handler surface
    /// inherits it as a follow-up.
    /// </summary>
    public async Task<data.@this> Run()
    {
        var srcAuth = await Source.Value!.Authorize(new Verb { Read = new ReadVerb() });
        if (srcAuth.Type?.ClrType.Exit() == true || !srcAuth.Success) return srcAuth;
        var dstAuth = await Destination.Value!.Authorize(new Verb { Write = new WriteVerb() });
        if (dstAuth.Type?.ClrType.Exit() == true || !dstAuth.Success) return dstAuth;
        return Files.Move(this);
    }
}
