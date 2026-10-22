namespace app.start.property.list;

/// <summary>The properties the command line gives, in the order written; a name given twice keeps the last.</summary>
public sealed class @this
{
    private readonly Dictionary<string, property.@this> _property = new();

    /// <summary>Each property, in the order written.</summary>
    public IEnumerable<property.@this> Items() => _property.Values;

    /// <summary>The property <paramref name="name"/> names, or null when the line gives none.</summary>
    public property.@this? this[string name] => _property.GetValueOrDefault(name);

    /// <summary>Takes the property <paramref name="name"/> names out of the list, answering it; null when the line
    /// gives none.</summary>
    public property.@this? Remove(string name) => _property.Remove(name, out var property) ? property : null;

    // the parse's: the last one written wins
    internal void Add(property.@this property) => _property[property.Name] = property;
}
