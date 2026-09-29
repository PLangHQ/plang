namespace PLang.Tests;

/// <summary>
/// A test-made action (<see cref="PLang.Tests.Shared.Make.Action"/>) born again in
/// <paramref name="step"/> — an action's step is a birth fact (init-only), so a test that makes the action
/// before its step hands it over this way: the same kind of action (a clause, a loop, a keep stays one), the
/// same program rows held by the new action.
/// </summary>
public static class ActionIn
{
    private static readonly string[] Rows =
        { "Module", "Name", "Synthetic", "Property", "Default", "Child", "Seed" };

    public static global::app.goal.step.action.@this In(this global::app.goal.step.action.@this action,
        global::app.goal.step.@this step)
    {
        var kind = action.GetType();
        var born = (global::app.goal.step.action.@this)System.Activator.CreateInstance(kind, nonPublic: true)!;
        foreach (var row in Rows)
        {
            var property = kind.GetProperty(row, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance)!;
            property.SetValue(born, property.GetValue(action));
        }
        kind.GetProperty("Step")!.SetValue(born, step);
        return born;
    }
}
