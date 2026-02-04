using System.Text.Json;

namespace PLang.Runtime;

public partial class MemoryStack
{
    private readonly Dictionary<string, ObjectValue?> _variables = new();
    private readonly EventCollection _events;
    
    public MemoryStack(EventCollection events)
    {
        _events = events;
    }
    
    public ObjectValue? Get(string variable)
    {
        var key = NormalizeKey(variable);
        return _variables.TryGetValue(key, out var val) ? val : null;
    }
    
    public void Set(string variable, object? value, TypeInfo? type = null)
    {
        var key = NormalizeKey(variable);
        
        // Clone before state and fire event
        ObjectValue? before = null;
        if (_variables.TryGetValue(key, out var existing))
        {
            before = new ObjectValue(existing.Name, CloneValue(existing.Value), existing.Type);
        }
        
        var after = new ObjectValue(key, value, type);
        
        _events.OnVariableChanging(key, before, after);
        
        _variables[key] = after;
        
        _events.OnVariableChanged(key, before, after);
    }
    
    public bool Contains(string variable)
    {
        var key = NormalizeKey(variable);
        return _variables.ContainsKey(key);
    }
    
    public void Remove(string variable)
    {
        var key = NormalizeKey(variable);
        _variables.Remove(key);
    }
    
    public void Clear()
    {
        _variables.Clear();
    }
    
    public IEnumerable<ObjectValue> All => _variables.Values.Where(v => v != null)!;
    
    public int Count => _variables.Count;
    
    private static string NormalizeKey(string variable)
    {
        return variable.Trim('%');
    }
    
    private static object? CloneValue(object? value)
    {
        if (value == null) return null;
        
        // For simple types, just return
        if (value is string or int or long or double or float or bool or DateTime or Guid)
            return value;
        
        // For complex types, serialize and deserialize to clone
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        return System.Text.Json.JsonSerializer.Deserialize<object>(json);
    }
}
