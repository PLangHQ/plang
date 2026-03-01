using PLang.Runtime2.modules;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
namespace PLang.Runtime2.Engine.Libraries;

/// <summary>
/// Smart collection of libraries. Owns walk-the-list action resolution.
/// Built-in library is always [0]. External DLLs can be added as additional libraries.
/// Replaces ActionRegistry entirely.
/// </summary>
public sealed class @this
{
    private readonly List<Library.@this> _libraries = new();

    /// <summary>
    /// The built-in library containing PLang's own actions. Always [0].
    /// </summary>
    public Library.@this BuiltIn => _libraries[0];

    public @this()
    {
        var builtIn = new Library.@this("builtin", typeof(@this).Assembly);
        builtIn.Discover("PLang.Runtime2.modules");
        _libraries.Add(builtIn);
    }

    // === Convenience delegates to BuiltIn (backward compat) ===

    public void Register(string module, string actionName, IAction action) =>
        BuiltIn.Register(module, actionName, action);

    public void RegisterCodeGenerated(string module, string actionName, Type type) =>
        BuiltIn.RegisterCodeGenerated(module, actionName, type);

    // === Resolution: walks all libraries, first match wins ===

    /// <summary>
    /// Resolves an action across all libraries. First match wins.
    /// Returns (action, null) on success, (null, error) on failure.
    /// </summary>
    public (ICodeGenerated? Action, IError? Error) GetCodeGenerated(
        string module, string actionName, PLangContext context)
    {
        foreach (var library in _libraries)
        {
            // Check explicit instances first (Register(instance) overrides discovered types)
            var action = library.Get(module, actionName);
            if (action != null)
            {
                if (action is not ICodeGenerated codeGenerated)
                    return (null, new ActionError(
                        $"Action '{module}.{actionName}' does not implement ICodeGenerated",
                        context, "ActionError", 500) { ActionModule = module, ActionName = actionName });
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

    public IEnumerable<IAction> All => _libraries.SelectMany(l => l.All);

    public IAction? Get(string module, string actionName) =>
        _libraries.Select(l => l.Get(module, actionName)).FirstOrDefault(h => h != null);

    public void Clear()
    {
        foreach (var lib in _libraries) lib.Clear();
    }

    // === Library management ===

    public void Add(Library.@this library) => _libraries.Add(library);
    public IReadOnlyList<Library.@this> Value => _libraries;
    public Library.@this this[int index] => _libraries[index];
}
