using System.Collections;

namespace App.Engine.Variables;

/// <summary>
/// A collection of Data properties.
/// Provides indexed and named access to child properties.
/// </summary>
public class Properties : IList<Data>
{
    private readonly List<Data> _items = new();

    public Data this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public Data? this[string name]
    {
        get => _items.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        set
        {
            var index = _items.FindIndex(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (value == null)
            {
                if (index >= 0)
                    _items.RemoveAt(index);
            }
            else if (index >= 0)
            {
                _items[index] = value;
            }
            else
            {
                _items.Add(value);
            }
        }
    }

    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(Data item) => _items.Add(item);
    public void Clear() => _items.Clear();
    public bool Contains(Data item) => _items.Contains(item);
    public bool Contains(string name) => _items.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    public void CopyTo(Data[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public IEnumerator<Data> GetEnumerator() => _items.GetEnumerator();
    public int IndexOf(Data item) => _items.IndexOf(item);
    public void Insert(int index, Data item) => _items.Insert(index, item);
    public bool Remove(Data item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Gets a property value by name.
    /// </summary>
    public T? Get<T>(string name)
    {
        var prop = this[name];
        return prop != null ? prop.GetValue<T>() : default;
    }

    /// <summary>
    /// Sets a property value by name.
    /// </summary>
    public void Set(string name, object? value, Type? type = null)
    {
        var existing = this[name];
        if (existing != null)
        {
            existing.Value = value;
            if (type != null)
                existing.Type = type;
        }
        else
        {
            Add(new Data(name, value, type));
        }
    }

    /// <summary>
    /// Removes a property by name.
    /// </summary>
    public bool Remove(string name)
    {
        var index = _items.FindIndex(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _items.RemoveAt(index);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Creates a copy of this Properties collection with independent item list.
    /// </summary>
    public Properties Clone()
    {
        var clone = new Properties();
        foreach (var item in _items)
            clone._items.Add(item);
        return clone;
    }

    /// <summary>
    /// Converts to a dictionary representation.
    /// </summary>
    public Dictionary<string, object?> ToDictionary()
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _items)
        {
            dict[item.Name] = item.Value;
        }
        return dict;
    }
}
