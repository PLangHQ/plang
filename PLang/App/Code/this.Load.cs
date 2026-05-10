using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using App.Errors;

namespace App.Code;

public sealed partial class @this
{
    /// <summary>
    /// Loads C# code into a fresh collectible ALC and returns a Runtime that can
    /// invoke methods on the entry type. Accepts a <c>.cs</c> source file (compiled
    /// in-memory with Roslyn) or a pre-built <c>.dll</c> (loaded directly). Each
    /// call produces a new Runtime — no caching at this layer.
    /// </summary>
    public async Task<Data.@this<Runtime.@this>> Load(Data.@this<FileSystem.Path> pathData)
    {
        var path = pathData.Value!;
        if (!path.Exists)
            return Data.@this<Runtime.@this>.FromError(new ActionError(
                $"File not found: {path.Relative}", "FileNotFound", 404));

        var fs = path.Context!.App.FileSystem;
        var ext = path.Extension?.ToLowerInvariant() ?? "";
        var name = path.FileNameWithoutExtension;

        Compiled.@this compiled;
        if (ext == ".cs")
        {
            var source = await fs.File.ReadAllTextAsync(path.Absolute);
            var result = Compile(source, name);
            if (!result.Success)
                return Data.@this<Runtime.@this>.FromError(result.Error!);
            compiled = result.Value!;
        }
        else
        {
            var bytes = await fs.File.ReadAllBytesAsync(path.Absolute);
            compiled = new Compiled.@this(bytes, name);
        }

        return compiled.Load();
    }

    private static Data.@this<Compiled.@this> Compile(string source, string name)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            assemblyName: $"PlangCode_{name}_{Guid.NewGuid():N}",
            syntaxTrees: new[] { tree },
            references: AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emit = compilation.Emit(ms);
        if (!emit.Success)
        {
            var errors = string.Join("\n", emit.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            return Data.@this<Compiled.@this>.FromError(new ActionError(
                "Compile failed:\n" + errors, "CompileFailed", 400));
        }
        return new Data.@this<Compiled.@this>(value: new Compiled.@this(ms.ToArray(), name));
    }
}
