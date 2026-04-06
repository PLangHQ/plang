using App.Variables;
using App.modules.file.providers;

namespace App.modules.file;

[Example("read file.txt, write to %content%", "Path=file.txt")]
[Example("read %path%, write to %data%", "Path=%path%")]
[Example("read file.txt, load vars, write to %content%", "Path=file.txt, ResolveVariables=true")]
[Action("read")]
public partial class Read : IContext
{
    public partial FileSystem.Path Path { get; init; }

    [Default(false)]
    public partial bool ResolveVariables { get; init; }

    [Provider]
    public partial IFileProvider Files { get; }

    public Task<Data.@this> Run()
    {
        var result = Files.Read(this);
        if (ResolveVariables && result.Success && result.Value is string content)
        {
            var resolved = Context.Variables.Resolve(content);
            return Task.FromResult(new Data.@this(result.Name, resolved, result.Type));
        }
        return Task.FromResult(result);
    }
}
