namespace PLang.Runtime;

public record TypeInfo(string ShortName)
{
    public string? FullName { get; set; }
    
    public Type? ToClrType()
    {
        if (!string.IsNullOrEmpty(FullName))
            return Type.GetType(FullName);
        
        return TypeMapping.FromSimpleName(ShortName);
    }
    
    public static TypeInfo FromClrType(Type type)
    {
        return new TypeInfo(TypeMapping.ToSimpleName(type))
        {
            FullName = type.FullName
        };
    }
    
    public static implicit operator TypeInfo(string shortName) => new(shortName);
}

public partial class ObjectValue
{
    public string Name { get; }
    public object? Value { get; set; }
    public TypeInfo? Type { get; }
    
    public ObjectValue(string name, object? value, TypeInfo? type = null)
    {
        Name = name;
        Value = value;
        Type = type ?? (value != null ? TypeInfo.FromClrType(value.GetType()) : null);
    }
    
    public T? GetValue<T>()
    {
        if (Value is T typed)
            return typed;
        
        if (Value == null)
            return default;
        
        try
        {
            return (T)Convert.ChangeType(Value, typeof(T));
        }
        catch
        {
            return default;
        }
    }
    
    public override string ToString()
    {
        return $"{Name}: {Value}";
    }
}
