using App.Types;
using App.Variables;
using App.modules.file.code;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using ReadVerb = global::App.FileSystem.Permission.Verb.Read;
using WriteVerb = global::App.FileSystem.Permission.Verb.Write;

namespace App.modules.file;

[ModuleDescription("Read, write, copy, move, delete, and list files through the configured filesystem abstraction")]
[System.ComponentModel.Description("Copy a file or folder from Source to Destination, optionally overwriting and including subfolders")]
[Action("copy", Cacheable = false)]
public partial class Copy : IContext
{
    public partial Data.@this<FileSystem.Path> Source { get; init; }
    public partial Data.@this<FileSystem.Path> Destination { get; init; }

    [Default(false)]
    public partial Data.@this<bool> Overwrite { get; init; }

    [Default(true)]
    public partial Data.@this<bool> IncludeSubfolders { get; init; }

    [Code]
    public partial IFile Files { get; }

    /// <summary>
    /// Authorize source (Read) then destination (Write) in sequence. v1
    /// degradation: two separate prompts on a fresh out-of-root pair.
    /// Bundled-consent UX is pinned by the C# Path.MoveTo/CopyTo path
    /// and tracked as a follow-up for the action-handler surface.
    /// </summary>
    public async Task<Data.@this> Run()
    {
        var srcAuth = await Source.Value!.Authorize(new Verb { Read = new ReadVerb() });
        if (srcAuth.Type?.ClrType.Exit() == true || !srcAuth.Success) return srcAuth;
        var dstAuth = await Destination.Value!.Authorize(new Verb { Write = new WriteVerb() });
        if (dstAuth.Type?.ClrType.Exit() == true || !dstAuth.Success) return dstAuth;
        return Files.Copy(this);
    }
}
