using System.Reflection;
using System.Text;
using System.Text.Json;

namespace PLang.Runtime;

public partial class SerializerRegistry
{
    private readonly Dictionary<string, ISerializer> _serializers = new();
    
    public SerializerRegistry()
    {
        // Register default serializers
        Register("json", new JsonStreamSerializer());
        Register("application/json", new JsonStreamSerializer());
        Register("text", new TextStreamSerializer());
        Register("text/plain", new TextStreamSerializer());
    }
    
    public ISerializer this[string name] => Get(name);
    
    public void Register(string name, ISerializer serializer)
    {
        _serializers[name.ToLowerInvariant()] = serializer;
    }
    
    public void Add(string pathOrTypeName)
    {
        if (pathOrTypeName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            LoadFromAssembly(pathOrTypeName);
        }
        else
        {
            LoadFromTypeName(pathOrTypeName);
        }
    }
    
    private void LoadFromAssembly(string dllPath)
    {
        var assembly = Assembly.LoadFrom(dllPath);
        var serializerTypes = assembly.GetTypes()
            .Where(t => typeof(ISerializer).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
        
        foreach (var type in serializerTypes)
        {
            var serializer = (ISerializer)Activator.CreateInstance(type)!;
            Register(serializer.Name, serializer);
        }
    }
    
    private void LoadFromTypeName(string typeName)
    {
        var type = Type.GetType(typeName);
        if (type == null)
            throw new SerializerNotFoundException(typeName);
        
        var serializer = (ISerializer)Activator.CreateInstance(type)!;
        Register(serializer.Name, serializer);
    }
    
    public ISerializer Get(string name)
    {
        if (_serializers.TryGetValue(name.ToLowerInvariant(), out var serializer))
            return serializer;
        
        throw new SerializerNotFoundException(name);
    }
    
    public bool Has(string name) => _serializers.ContainsKey(name.ToLowerInvariant());
}

public interface ISerializer
{
    string Name { get; }
    
    // Stream-based (primary)
    void Serialize(object data, Stream stream);
    T? Deserialize<T>(Stream stream);
    object? Deserialize(Stream stream, Type type);
    
    // Byte-based (convenience)
    byte[] SerializeToBytes(object data);
    T? DeserializeFromBytes<T>(byte[] data);
}

public class JsonStreamSerializer : ISerializer
{
    public string Name => "json";
    
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
    
    public void Serialize(object data, Stream stream)
    {
        System.Text.Json.JsonSerializer.Serialize(stream, data, _options);
    }
    
    public T? Deserialize<T>(Stream stream)
    {
        return System.Text.Json.JsonSerializer.Deserialize<T>(stream, _options);
    }
    
    public object? Deserialize(Stream stream, Type type)
    {
        return System.Text.Json.JsonSerializer.Deserialize(stream, type, _options);
    }
    
    public byte[] SerializeToBytes(object data)
    {
        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data, _options);
    }
    
    public T? DeserializeFromBytes<T>(byte[] data)
    {
        return System.Text.Json.JsonSerializer.Deserialize<T>(data, _options);
    }
}

public class TextStreamSerializer : ISerializer
{
    public string Name => "text";
    
    public void Serialize(object data, Stream stream)
    {
        var bytes = Encoding.UTF8.GetBytes(data?.ToString() ?? "");
        stream.Write(bytes, 0, bytes.Length);
    }
    
    public T? Deserialize<T>(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var text = reader.ReadToEnd();
        return (T)Convert.ChangeType(text, typeof(T));
    }
    
    public object? Deserialize(Stream stream, Type type)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var text = reader.ReadToEnd();
        return Convert.ChangeType(text, type);
    }
    
    public byte[] SerializeToBytes(object data)
    {
        return Encoding.UTF8.GetBytes(data?.ToString() ?? "");
    }
    
    public T? DeserializeFromBytes<T>(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);
        return (T)Convert.ChangeType(text, typeof(T));
    }
}
