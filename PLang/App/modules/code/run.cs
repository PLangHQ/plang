using App.Variables;

namespace App.modules.code;

/// <summary>
/// Compiles a .cs file (or loads a .dll) and invokes <c>Start(Data data)</c>
/// on its entry class. Single contract: one input, one result.
/// </summary>
[System.ComponentModel.Description(
    "Compile and run a .cs file (or load and run a .dll). " +
    "The script's entry class is instantiated and its Start(Data) method is invoked. " +
    "The script's return value flows back through %__data__%.")]
[Example("run mycode.cs",
    "code.run Path([path] mycode.cs)")]
[Example("run mycode.cs %!data%, write to %answer%",
    "code.run Path([path] mycode.cs), Data([object] %!data%) | " +
    "variable.set Name([string] %answer%), Value([object] %__data__%)")]
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    public partial Data.@this<FileSystem.Path> Path { get; init; }
    public partial Data.@this? Data { get; init; }

    public async Task<Data.@this> Run()
    {
        var code = new Code.Runner.@this(Path);
        return await code.Start(Data ?? global::App.Data.@this.Ok(null));
    }
}
