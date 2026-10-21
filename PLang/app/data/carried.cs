namespace app.data;

/// <summary>
/// What a variable's value carries with it — its properties (<c>!</c>) and the members a program added to it (<c>.</c>).
/// A face of the same variable (a typed view) shares it; a copy (a new variable over the same value) takes its own,
/// the bag and the list copied, what they hold shared. What travels with a value is added here, once.
/// </summary>
internal sealed class carried
{
    public Properties Properties { get; set; } = new();

    public global::app.type.item.kept.list.@this Kept { get; set; } = new();

    /// <summary>A copy for a new variable over the same value.</summary>
    public carried Copy() => new() { Properties = Properties.Clone(), Kept = Kept.Copy() };
}
