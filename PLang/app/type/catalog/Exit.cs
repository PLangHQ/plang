namespace app.type.catalog;

/// <summary>
/// Pure-verb predicate. The only engine-side discriminator for "this Data
/// exits the goal." Step loop, Step.RunAsync, Goal.RunFrom all use it
/// (via <c>Data.ShouldExit</c>) — no per-kind callback classes, no decomposition
/// of the Data's Value.
/// </summary>
public static class TypeExitExtensions
{
    public static bool Exit(this System.Type? clrType)
        => clrType != null && typeof(global::app.IExitsGoal).IsAssignableFrom(clrType);
}
