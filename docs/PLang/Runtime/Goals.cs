using System.Collections;
using System.Text.Json;

namespace PLang.Runtime;

public partial class Goals : IEnumerable<Goal>
{
    private readonly List<Goal> _goals = new();
    
    public int Count => _goals.Count;
    
    public Goal? FirstOrDefault(Func<Goal, bool> predicate)
        => _goals.FirstOrDefault(predicate);
    
    public Goal this[int index] => _goals[index];
    
    public Goal? this[string path] => _goals.FirstOrDefault(g => g.Path == path);
    
    public void Add(Goal goal) => _goals.Add(goal);
    
    public void Remove(Goal goal) => _goals.Remove(goal);
    
    public void Clear() => _goals.Clear();
    
    public void Load(string path)
    {
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<GoalData>(json, options);
        if (data == null) return;
        
        var steps = data.Steps.Select(s => new Step(
            s.Line,
            s.Text,
            s.Module,
            s.Method
        )).ToList();
        
        _goals.Add(new Goal(data.Path, steps));
    }
    
    public void LoadDirectory(string directory, string pattern = "*.pr")
    {
        var files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
        foreach (var file in files)
        {
            Load(file);
        }
    }
    
    /// <summary>
    /// Enable CallStack on all loaded goals
    /// </summary>
    public void EnableCallStack()
    {
        foreach (var goal in _goals)
        {
            goal.EnableCallStack();
        }
    }
    
    public IEnumerator<Goal> GetEnumerator() => _goals.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
