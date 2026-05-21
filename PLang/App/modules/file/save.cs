using App.Types;
using App.Variables;
using App.modules.file.code;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using WriteVerb = global::App.FileSystem.Permission.Verb.Write;

namespace App.modules.file;

[System.ComponentModel.Description("Write Value to a file at Path, creating directories as needed")]
[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial Data.@this<FileSystem.Path> Path { get; init; }
    public partial Data.@this? Value { get; init; }

    [Code]
    public partial IFile Files { get; }

    public async Task<Data.@this> Run()
    {
        var auth = await Path.Value!.Authorize(new Verb { Write = new WriteVerb() });
        if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
        return await Files.Save(this);
    }
}
