using app.variables;
using app.modules.file.code;

namespace app.modules.file;

/// <summary>
/// Reads a file and returns its content as Data.
/// When ResolveVariables is true, %var% patterns in the content are resolved (with infrastructure variables blocked for security).
/// </summary>
[System.ComponentModel.Description("Read a file's content; optionally resolve %var% patterns in the text before returning")]
[Example("read file.txt, write to %content%",
    "file.read Path([path] file.txt) | variable.set Name([string] %content%), Value([object] %__data__%)")]
[Action("read")]
public partial class Read : IContext
{
    public partial data.@this<filesystem.path> Path { get; init; }

    [Default(false)]
    public partial data.@this<bool> ResolveVariables { get; init; }

    [Code]
    public partial IFile Files { get; }

    public Task<data.@this> Run()
    {
        var result = Files.Read(this);
        if (ResolveVariables.Value && result.Success && result.Value is string content)
        {
            // skipInfrastructure: file content is untrusted — don't resolve %!app% etc.
            var resolved = Context.Variables.Resolve(content, skipInfrastructure: true);
            return Task.FromResult(new data.@this(result.Name, resolved, result.Type));
        }
        return Task.FromResult(result);
    }
}
