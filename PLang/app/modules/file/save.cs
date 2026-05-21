using app.variables;
using app.modules.file.code;
using app.types;
using Verb = global::app.filesystem.permission.verb.@this;
using WriteVerb = global::app.filesystem.permission.verb.Write;

namespace app.modules.file;

[System.ComponentModel.Description("Write Value to a file at Path, creating directories as needed")]
[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial data.@this<filesystem.path> Path { get; init; }
    public partial data.@this? Value { get; init; }

    [Code]
    public partial IFile Files { get; }

    public async Task<data.@this> Run()
    {
        var auth = await Path.Value!.Authorize(new Verb { Write = new WriteVerb() });
        if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
        return await Files.Save(this);
    }
}
