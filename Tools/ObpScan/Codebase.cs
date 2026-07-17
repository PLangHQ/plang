using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace ObpScan;

/// <summary>The loaded codebase — owns opening the workspace and finding types by name-substring.
/// Consumers ask it for TypeScans; the MSBuild/Roslyn loading lives here alone.</summary>
public sealed class Codebase : IDisposable
{
    private readonly MSBuildWorkspace _workspace;
    private readonly Project _project;

    private Codebase(MSBuildWorkspace workspace, Project project)
    {
        _workspace = workspace;
        _project = project;
    }

    public static async Task<Codebase> Load(string projectPath)
    {
        var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, e) =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                Console.Error.WriteLine($"[load] {e.Diagnostic.Message}");
        };
        Console.Error.WriteLine($"loading {projectPath} …");
        var project = await workspace.OpenProjectAsync(projectPath);
        return new Codebase(workspace, project);
    }

    /// <summary>The source-defined types whose full name contains <paramref name="substring"/>.</summary>
    public async Task<IReadOnlyList<TypeScan>> Find(string substring)
    {
        var compilation = await _project.GetCompilationAsync()
            ?? throw new InvalidOperationException("no compilation");
        var all = new List<INamedTypeSymbol>();
        Collect(compilation.Assembly.GlobalNamespace, all);
        return all
            .Where(t => t.Locations.Any(l => l.IsInSource))
            .Where(t => $"{t.ContainingNamespace?.ToDisplayString()}.{t.Name}"
                        .Contains(substring, StringComparison.OrdinalIgnoreCase))
            .Select(t => new TypeScan(t, _project.Solution))
            .ToList();
    }

    private static void Collect(INamespaceSymbol ns, List<INamedTypeSymbol> acc)
    {
        acc.AddRange(ns.GetTypeMembers());
        foreach (var child in ns.GetNamespaceMembers()) Collect(child, acc);
    }

    public void Dispose() => _workspace.Dispose();
}
