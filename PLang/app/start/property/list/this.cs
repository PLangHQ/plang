namespace app.start.property.list;

/// <summary>The properties the command line gives, in the order written; one given twice (the same name, both flags or
/// both values) keeps the last.</summary>
public sealed class @this
{
    private readonly Dictionary<(string Name, bool IsFlag), property.@this> _property = new();

    /// <summary>Each property, in the order written.</summary>
    public IEnumerable<property.@this> Items() => _property.Values;

    /// <summary>Whether the line gives the value <paramref name="name"/> (<c>path=…</c>), a flag aside.</summary>
    public bool Has(string name) => _property.ContainsKey((name, false));

    // the parse's: the last one written wins
    internal void Add(property.@this property) => _property[(property.Name, property.IsFlag)] = property;
}
