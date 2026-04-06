using App.Variables;
using App.modules.file.providers;

namespace App.modules.file;

[Example("save %content% to file.txt", "Path=file.txt, Value=%content%")]
[Example("save %data% to %path%", "Path=%path%, Value=%data%")]
[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial PLangPath Path { get; init; }
    public partial Data? Value { get; init; }

    [Provider]
    public partial IFileProvider Files { get; }

    public Task<Data> Run() => Files.Save(this);
}
