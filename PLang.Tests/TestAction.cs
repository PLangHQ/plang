namespace PLang.Tests;

/// <summary>
/// Builds Action.@this objects for tests so they go through the same
/// ExecuteAsync → __Resolve → Run() pipeline as runtime .pr dispatch.
/// </summary>
public static class TestAction
{
    public static global::app.goals.goal.steps.step.actions.action.@this Create(string module, string action,
        params (string name, object? value)[] parameters)
    {
        return new global::app.goals.goal.steps.step.actions.action.@this
        {
            Module = module,
            ActionName = action,
            Parameters = parameters
                .Select(p => new global::app.data.@this(p.name, p.value))
                .ToList()
        };
    }

    /// <summary>Wraps a typed value in data.@this&lt;T&gt; for direct action construction in tests.</summary>
    public static global::app.data.@this<T> D<T>(T value) => new("", value);

    /// <summary>Wraps an untyped value in data.@this for direct action construction in tests.</summary>
    public static global::app.data.@this D(object? value) => new("", value);
}
