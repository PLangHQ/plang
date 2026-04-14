using App.Variables;
using App.modules.file.providers;

namespace App.modules.file;

/// <summary>
/// Reads a file and returns its content as Data.
/// When ResolveVariables is true, %var% patterns in the content are resolved (with infrastructure variables blocked for security).
/// </summary>
[Example("read file.txt, write to %content%", "Path=file.txt")]
[Example("read %path%, write to %data%", "Path=%path%")]
[Example("read file.txt, load vars, write to %content%", "Path=file.txt, ResolveVariables=true")]
[Action("read")]
public partial class Read : IContext
{
    public partial Data.@this<FileSystem.Path> Path { get; init; }

    [Default(false)]
    public partial Data.@this<bool> ResolveVariables { get; init; }

    [Provider]
    public partial IFileProvider Files { get; }

    public Task<Data.@this> Run()
    {
        var result = Files.Read(this);
        if (ResolveVariables.Value && result.Success && result.Value is string content)
        {
            // skipInfrastructure: file content is untrusted — don't resolve %!app% etc.
            var resolved = Context.Variables.Resolve(content, skipInfrastructure: true);
            return Task.FromResult(new Data.@this(result.Name, resolved, result.Type));
        }
        return Task.FromResult(result);
    }
}
