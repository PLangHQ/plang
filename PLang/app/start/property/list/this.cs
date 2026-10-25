namespace app.start.property.list;

/// <summary>The properties the command line gives, in the order written; one given twice (the same name, both flags or
/// both values) keeps the last. Names are one whatever their case: the same name written in another case is refused.</summary>
public sealed class @this
{
    private readonly Dictionary<(string Name, bool IsFlag), property.@this> _property = new(new Key());

    /// <summary>Each property, in the order written.</summary>
    public IEnumerable<property.@this> Items() => _property.Values;

    /// <summary>Whether the line gives the value <paramref name="name"/> (<c>path=…</c>), a flag aside.</summary>
    public bool Has(string name) => _property.ContainsKey((name, false));

    // the parse's: the last one written wins; one written before in another case is the line's refusal
    internal void Add(property.@this property)
    {
        if (_property.TryGetValue((property.Name, property.IsFlag), out var before)
            && !string.Equals(before.Name, property.Name, StringComparison.Ordinal))
        {
            var dashes = property.IsFlag ? "--" : "";
            throw new global::app.error.AppException(global::app.start.@this.Refused(
                $"'{dashes}{before.Name}' and '{dashes}{property.Name}' are one {(property.IsFlag ? "setting" : "value")} written twice in different case: write it once."));
        }
        _property[(property.Name, property.IsFlag)] = property;
    }

    // a property's key: its name whatever its case, and whether it is a flag
    private sealed class Key : IEqualityComparer<(string Name, bool IsFlag)>
    {
        public bool Equals((string Name, bool IsFlag) x, (string Name, bool IsFlag) y)
            => x.IsFlag == y.IsFlag && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Name, bool IsFlag) key)
            => System.HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(key.Name), key.IsFlag);
    }
}
