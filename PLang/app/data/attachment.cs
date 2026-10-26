namespace app.data;

/// <summary>
/// What a variable's value carries with it — its properties (<c>!</c>) and the properties a program added to it (<c>.</c>).
/// A face of the same variable (a typed view) shares it; a copy (a new variable over the same value) takes its own,
/// the bag and the list copied, what they hold shared. What travels with a value is added here, once.
/// </summary>
internal sealed class attachment
{
    /// <summary>The value's properties — made the first time they are asked for; the first made is the one kept.</summary>
    public Properties Properties
    {
        get => System.Threading.LazyInitializer.EnsureInitialized(ref _properties, static () => new());
        set => _properties = value;
    }
    private Properties? _properties;

    /// <summary>Whether the value carries any property — asked without making the bag.</summary>
    public bool HasProperties => _properties is { Count: > 0 };

    /// <summary>The properties a program added — made the first time one is asked for; the first made is the one kept.</summary>
    public global::app.type.item.property.list.@this Property
    {
        get => System.Threading.LazyInitializer.EnsureInitialized(ref _property, static () => new());
        set => _property = value;
    }
    private global::app.type.item.property.list.@this? _property;

    /// <summary>Whether a program added any property — asked without making the list.</summary>
    public bool HasProperty => _property is { Count: > 0 };

    /// <summary>A copy for a new variable over the same value.</summary>
    public attachment Copy() => new() { _properties = _properties?.Clone(), _property = _property?.Copy() };
}
