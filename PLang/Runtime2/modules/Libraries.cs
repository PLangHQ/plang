using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;

namespace PLang.Runtime2.modules;

/// <summary>
/// Smart collection of libraries. Owns walk-the-list handler resolution.
/// Built-in library is always [0]. External DLLs can be added as additional libraries.
/// Replaces ActionRegistry entirely.
/// </summary>
public sealed class Libraries
{
    private readonly List<Library> _libraries = new();

    /// <summary>
    /// The built-in library containing PLang's own action handlers. Always [0].
    /// </summary>
    public Library BuiltIn => _libraries[0];

    public Libraries()
    {
        var builtIn = new Library("builtin", typeof(Libraries).Assembly);
        builtIn.Discover("PLang.Runtime2.modules");
        _libraries.Add(builtIn);
    }

    // === Convenience delegates to BuiltIn (backward compat) ===

    public void Register(string module, string actionName, IClass handler) =>
        BuiltIn.Register(module, actionName, handler);

    public void RegisterCodeGenerated(string module, string actionName, Type type) =>
        BuiltIn.RegisterCodeGenerated(module, actionName, type);

    // === Resolution: walks all libraries, first match wins ===

    /// <summary>
    /// Resolves a handler across all libraries. First match wins.
    /// Returns (handler, null) on success, (null, error) on failure.
    /// </summary>
    public (ICodeGenerated? Handler, IError? Error) GetCodeGenerated(
        string module, string actionName, PLangContext context)
    {
        foreach (var library in _libraries)
        {
            // Check explicit instances first (Register(instance) overrides discovered types)
            var handler = library.Get(module, actionName);
            if (handler != null)
            {
                if (handler is not ICodeGenerated codeGenerated)
                    return (null, new ActionError(
                        $"Handler '{module}.{actionName}' does not implement ICodeGenerated",
                        context, "HandlerError", 500) { ActionModule = module, ActionName = actionName });
                return (codeGenerated, null);
            }

            // Per-call instantiation from registered Types
            var codeGen = library.GetCodeGenerated(module, actionName);
            if (codeGen != null)
                return (codeGen, null);
        }

        return (null, ActionError.NotFound($"Action '{module}.{actionName}'", context));
    }

    // === Aggregate queries ===

    public bool Contains(string module, string actionName) =>
        _libraries.Any(l => l.Contains(module, actionName));

    public bool Contains(string module) =>
        _libraries.Any(l => l.Contains(module));

    public IEnumerable<string> Modules =>
        _libraries.SelectMany(l => l.Modules).Distinct(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> GetActions(string module) =>
        _libraries.SelectMany(l => l.GetActions(module)).Distinct(StringComparer.OrdinalIgnoreCase);

    public Type? GetActionType(string module, string actionName) =>
        _libraries.Select(l => l.GetActionType(module, actionName)).FirstOrDefault(t => t != null);

    public int Count => _libraries.Sum(l => l.Count);

    public IEnumerable<IClass> All => _libraries.SelectMany(l => l.All);

    public IClass? Get(string module, string actionName) =>
        _libraries.Select(l => l.Get(module, actionName)).FirstOrDefault(h => h != null);

    public void Clear()
    {
        foreach (var lib in _libraries) lib.Clear();
    }

    // === Library management ===

    public void Add(Library library) => _libraries.Add(library);
    public IReadOnlyList<Library> Value => _libraries;
    public Library this[int index] => _libraries[index];
}
