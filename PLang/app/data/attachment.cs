namespace app.data;

/// <summary>
/// What a variable's value carries with it — its properties (<c>!</c>) and the members a program added to it (<c>.</c>).
/// A face of the same variable (a typed view) shares it; a copy (a new variable over the same value) takes its own,
/// the bag and the list copied, what they hold shared. What travels with a value is added here, once.
/// </summary>
internal sealed class attachment
{
    public Properties Properties { get; set; } = new();

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
    public attachment Copy() => new() { Properties = Properties.Clone(), _kept = _kept?.Copy() };
}
