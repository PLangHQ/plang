using System.Reflection;

namespace PLang.Runtime;

public static partial class ModuleRegistry
{
    private static readonly Dictionary<string, IModule> _modules = new();
    
    public static void Register(string name, IModule module)
    {
        _modules[name.ToLowerInvariant()] = module;
    }
    
    public static void Register<T>(string name) where T : IModule, new()
    {
        _modules[name.ToLowerInvariant()] = new T();
    }
    
    public static void LoadFromAssembly(string dllPath)
    {
        var assembly = Assembly.LoadFrom(dllPath);
        var moduleTypes = assembly.GetTypes()
            .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
        
        foreach (var type in moduleTypes)
        {
            var module = (IModule)Activator.CreateInstance(type)!;
            Register(module.Name, module);
        }
    }
    
    public static IModule Get(string name)
    {
        if (_modules.TryGetValue(name.ToLowerInvariant(), out var module))
            return module;
        
        throw new ModuleNotFoundException(name);
    }
    
    public static bool Has(string name)
    {
        return _modules.ContainsKey(name.ToLowerInvariant());
    }
    
    public static IEnumerable<string> ModuleNames => _modules.Keys;
}

public interface IModule
{
    string Name { get; }
    
    // Context properties - set before Execute is called
    Engine Engine { get; set; }
    Goal Goal { get; set; }
    Step Step { get; set; }
    
    // Returns GoalResult, not object?
    Task<GoalResult> Execute(string method, object? data);
}

public abstract class BaseModule : IModule
{
    public abstract string Name { get; }
    
    public Engine Engine { get; set; } = null!;
    public Goal Goal { get; set; } = null!;
    public Step Step { get; set; } = null!;
    
    // Convenience accessors
    protected MemoryStack MemoryStack => Engine.MemoryStack;
    protected PLangAppContext System => Engine.System;
    protected PLangContext User => Engine.User;
    protected IO Out => Engine.Out;
    protected IO In => Engine.In;
    protected SerializerRegistry Serializers => Engine.Serializers;
    
    public abstract Task<GoalResult> Execute(string method, object? data);
}
