namespace PLang.Runtime;

public static partial class TypeMapping
{
    private static readonly Dictionary<string, Type> _toClr = new()
    {
        ["string"] = typeof(string),
        ["int"] = typeof(int),
        ["long"] = typeof(long),
        ["double"] = typeof(double),
        ["float"] = typeof(float),
        ["bool"] = typeof(bool),
        ["datetime"] = typeof(DateTime),
        ["guid"] = typeof(Guid),
        ["object"] = typeof(object),
        ["list"] = typeof(List<object>),
        ["dict"] = typeof(Dictionary<string, object>),
        ["byte"] = typeof(byte),
        ["short"] = typeof(short),
        ["decimal"] = typeof(decimal),
        ["char"] = typeof(char),
        ["timespan"] = typeof(TimeSpan),
    };
    
    private static readonly Dictionary<Type, string> _toSimple;
    
    static TypeMapping()
    {
        _toSimple = _toClr.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
    }
    
    public static Type? FromSimpleName(string name)
        => _toClr.TryGetValue(name.ToLowerInvariant(), out var type) ? type : null;
    
    public static string ToSimpleName(Type type)
        => _toSimple.TryGetValue(type, out var name) ? name : type.Name;
    
    public static void Register(string simpleName, Type clrType)
    {
        _toClr[simpleName.ToLowerInvariant()] = clrType;
        _toSimple[clrType] = simpleName.ToLowerInvariant();
    }
}
