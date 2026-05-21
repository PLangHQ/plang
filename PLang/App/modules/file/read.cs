using App.Types;
using App.Variables;
using App.modules.file.code;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using ReadVerb = global::App.FileSystem.Permission.Verb.Read;

namespace App.modules.file;

/// <summary>
/// Reads a file and returns its content as Data.
/// When ResolveVariables is true, %var% patterns in the content are resolved (with infrastructure variables blocked for security).
/// Calls <see cref="FileSystem.Path.Authorize"/> first — out-of-root paths
/// prompt for consent (stateful) or surface as <c>Data&lt;Ask&gt;</c> + Snapshot
/// (stateless); the engine short-circuits via the step-loop's ShouldExit().
/// </summary>
[System.ComponentModel.Description("Read a file's content; optionally resolve %var% patterns in the text before returning")]
[Example("read file.txt, write to %content%",
    "file.read Path([path] file.txt) | variable.set Name([string] %content%), Value([object] %!data%)")]
[Action("read")]
public partial class Read : IContext
{
    public partial Data.@this<FileSystem.Path> Path { get; init; }

    [Default(false)]
    public partial Data.@this<bool> ResolveVariables { get; init; }

    [Code]
    public partial IFile Files { get; }

    public async Task<Data.@this> Run()
    {
        var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
        if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;

        var result = Files.Read(this);
        if (ResolveVariables.Value && result.Success && result.Value is string content)
        {
            // skipInfrastructure: file content is untrusted — don't resolve %!app% etc.
            var resolved = Context.Variables.Resolve(content, skipInfrastructure: true);
            return new Data.@this(result.Name, resolved, result.Type);
        }
        return result;
    }
}
