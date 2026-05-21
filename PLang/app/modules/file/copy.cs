using app.variables;
using app.modules.file.code;
using app.types;
using Verb = global::app.filesystem.permission.verb.@this;
using ReadVerb = global::app.filesystem.permission.verb.Read;
using WriteVerb = global::app.filesystem.permission.verb.Write;

namespace app.modules.file;

[ModuleDescription("Read, write, copy, move, delete, and list files through the configured filesystem abstraction")]
[System.ComponentModel.Description("Copy a file or folder from Source to Destination, optionally overwriting and including subfolders")]
[Action("copy", Cacheable = false)]
public partial class Copy : IContext
{
    public partial data.@this<filesystem.path> Source { get; init; }
    public partial data.@this<filesystem.path> Destination { get; init; }

    [Default(false)]
    public partial data.@this<bool> Overwrite { get; init; }

    [Default(true)]
    public partial data.@this<bool> IncludeSubfolders { get; init; }

    [Code]
    public partial IFile Files { get; }

    /// <summary>
    /// Authorize source (Read) then destination (Write) in sequence. v1
    /// degradation: two separate prompts on a fresh out-of-root pair.
    /// Bundled-consent UX is pinned by the C# path.MoveTo/CopyTo path
    /// and tracked as a follow-up for the action-handler surface.
    /// </summary>
    public async Task<data.@this> Run()
    {
        var srcAuth = await Source.Value!.Authorize(new Verb { Read = new ReadVerb() });
        if (srcAuth.Type?.ClrType.Exit() == true || !srcAuth.Success) return srcAuth;
        var dstAuth = await Destination.Value!.Authorize(new Verb { Write = new WriteVerb() });
        if (dstAuth.Type?.ClrType.Exit() == true || !dstAuth.Success) return dstAuth;
        return Files.Copy(this);
    }
}
