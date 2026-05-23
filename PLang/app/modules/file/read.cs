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

    public async Task<data.@this<object>> Run()
    {
        if (!Path.Success) return global::app.data.@this<object>.From(Path);   // codeanalyzer v1 F4 — typed scheme error, not an NRE
        var read = await Path.Value!.ReadText();
        if (!read.Success || read.Type?.ClrType.Exit() == true) return global::app.data.@this<object>.From(read);
        if (ResolveVariables.Value && read.Value is string content)
        {
            var resolved = Context.Variables.Resolve(content, skipInfrastructure: true);
            return new global::app.data.@this<object>(read.Name, resolved, read.Type);
        }
        return global::app.data.@this<object>.From(read);
    }
}
