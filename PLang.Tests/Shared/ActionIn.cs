namespace PLang.Tests;

/// <summary>
/// A test-made action (<see cref="PLang.Tests.Shared.Make.Action"/>, <see cref="TestAction.Create"/>) born again in
/// <paramref name="step"/> — an action's step is a birth fact (init-only), so a test that makes the action
/// before its step hands it over this way, the same program rows held by the new action.
/// </summary>
public static class ActionIn
{
    public static global::app.goal.step.action.@this In(this global::app.goal.step.action.@this action,
        global::app.goal.step.@this step)
        => action is global::app.goal.step.action.clause.@this
            ? new global::app.goal.step.action.clause.@this
            {
                Module = action.Module, Name = action.Name, Step = step, Synthetic = action.Synthetic,
                Property = action.Property, Default = action.Default, Child = action.Child, Seed = action.Seed,
            }
            : new global::app.goal.step.action.@this
            {
                Module = action.Module, Name = action.Name, Step = step, Synthetic = action.Synthetic,
                Property = action.Property, Default = action.Default, Child = action.Child, Seed = action.Seed,
            };
}
