using app.variables;
using app.types;

namespace app.modules.file;

/// <summary>
/// Reads a file and returns its content as Data.
/// When ResolveVariables is true, %var% patterns in the content are resolved
/// (with infrastructure variables blocked for security).
///
/// The Authorize call lives inside the Path verb impl (FilePath.ReadText etc.) —
/// the handler no longer carries an authorization preamble. This is the
/// codeanalyzer v2 #1 fix: gate centralised, not duplicated.
/// </summary>
[System.ComponentModel.Description("Read a file's content; optionally resolve %var% patterns in the text before returning")]
[Example("read file.txt, write to %content%",
    "file.read Path([path] file.txt) | variable.set Name([string] %content%), Value([object] %!data%)")]
[Action("read")]
public partial class Read : IContext
{
    public partial data.@this<path> Path { get; init; }

    [Default(false)]
    public partial data.@this<bool> ResolveVariables { get; init; }

    public async Task<data.@this> Run()
    {
        var read = await Path.Value!.ReadText();
        if (!read.Success || read.Type?.ClrType.Exit() == true) return read;
        if (ResolveVariables.Value && read.Value is string content)
        {
            var resolved = Context.Variables.Resolve(content, skipInfrastructure: true);
            return new data.@this(read.Name, resolved, read.Type);
        }
        return read;
    }
}
