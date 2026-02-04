namespace PLang.Runtime;

public partial class Properties : List<ObjectValue>
{
    public object? this[string name]
    {
        get => this.FirstOrDefault(p => p.Name == name)?.Value;
        set
        {
            var existing = this.FirstOrDefault(p => p.Name == name);
            if (existing != null)
                existing.Value = value;
            else
                Add(new ObjectValue(name, value));
        }
    }
    
    public void Add(string name, object? value)
        => Add(new ObjectValue(name, value));
    
    public bool Contains(string name)
        => this.Any(p => p.Name == name);
    
    public void Remove(string name)
    {
        var item = this.FirstOrDefault(p => p.Name == name);
        if (item != null)
            base.Remove(item);
    }
}
