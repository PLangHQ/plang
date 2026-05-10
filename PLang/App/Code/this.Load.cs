using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using App.Errors;

namespace App.Code;

public sealed partial class @this
{
    // App-scoped Runtime cache. Compile-once-per-app: same source-hash → same
    // Runtime for the lifetime of the App. Cache eviction is deferred to
    // App.DisposeAsync (see this.cs), which iterates these and unloads each
    // ALC. Keyed by SHA-256 of the source bytes (works for both .cs source
    // text and pre-compiled .dll bytes — we hash whatever goes into the ALC).
    private readonly ConcurrentDictionary<string, Runtime.@this> _runtimesByHash = new();

    /// <summary>
    /// Loads C# code into a collectible ALC and returns a Runtime that can
    /// invoke methods on the entry type. Accepts a <c>.cs</c> source file
    /// (compiled in-memory with Roslyn) or a pre-built <c>.dll</c> (loaded
    /// directly). Same-source calls share the same Runtime — compile once,
    /// keep in memory, disposed at App shutdown.
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
            var hash = Sha256(source);
            if (_runtimesByHash.TryGetValue(hash, out var hit))
                return new Data.@this<Runtime.@this>(value: hit);

            var result = Compile(source, name, hash);
            if (!result.Success) return Data.@this<Runtime.@this>.FromError(result.Error!);
            compiled = result.Value!;
        }
        else
        {
            var bytes = await fs.File.ReadAllBytesAsync(path.Absolute);
            var hash = Sha256(bytes);
            if (_runtimesByHash.TryGetValue(hash, out var hit))
                return new Data.@this<Runtime.@this>(value: hit);

            compiled = new Compiled.@this(bytes, name, hash);
        }

        var loaded = compiled.Load();
        if (!loaded.Success) return loaded;

        // Race: if another caller cached the same hash between TryGetValue and
        // here, prefer the winner and dispose ours.
        var stored = _runtimesByHash.GetOrAdd(compiled.Hash, loaded.Value!);
        if (!ReferenceEquals(stored, loaded.Value)) await loaded.Value!.DisposeAsync();
        return new Data.@this<Runtime.@this>(value: stored);
    }

    private static Data.@this<Compiled.@this> Compile(string source, string name, string hash)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            assemblyName: $"PlangCode_{name}_{hash[..8]}",
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
        return new Data.@this<Compiled.@this>(value: new Compiled.@this(ms.ToArray(), name, hash));
    }

    private static string Sha256(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Sha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes));
}
