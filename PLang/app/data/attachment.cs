namespace app.data;

/// <summary>
/// What a variable's value carries with it — its properties (<c>!</c>) and the members a program added to it (<c>.</c>).
/// A face of the same variable (a typed view) shares it; a copy (a new variable over the same value) takes its own,
/// the bag and the list copied, what they hold shared. What travels with a value is added here, once.
/// </summary>
internal sealed class attachment
{
    /// <summary>The value's properties — made the first time they are asked for; the first made is the one kept.</summary>
    public Properties Properties
    {
        get => _properties ?? System.Threading.Interlocked.CompareExchange(ref _properties, new(), null) ?? _properties;
        set => _properties = value;
    }
    private Properties? _properties;

    /// <summary>Whether the value carries any property — asked without making the bag.</summary>
    public bool HasProperties => _properties is { Count: > 0 };

    /// <summary>The members a program added — made the first time one is asked for; the first made is the one kept.</summary>
    public global::app.type.item.kept.list.@this Kept
    {
        get => _kept ?? System.Threading.Interlocked.CompareExchange(ref _kept, new(), null) ?? _kept;
        set => _kept = value;
    }
    private global::app.type.item.kept.list.@this? _kept;

    /// <summary>Whether a program added any member — asked without making the list.</summary>
    public bool HasKept => _kept is { Count: > 0 };

    /// <summary>A copy for a new variable over the same value.</summary>
    public attachment Copy() => new() { _properties = _properties?.Clone(), _kept = _kept?.Copy() };
}
