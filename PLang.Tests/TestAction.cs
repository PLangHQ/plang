namespace PLang.Tests;

/// <summary>
/// Builds Action.@this objects for tests so they go through the same
/// ExecuteAsync → __Resolve → Run() pipeline as runtime .pr dispatch.
/// </summary>
public static class TestAction
{
    public static global::app.Goals.Goal.Steps.Step.Actions.Action.@this Create(string module, string action,
        params (string name, object? value)[] parameters)
    {
        return new global::app.Goals.Goal.Steps.Step.Actions.Action.@this
        {
            Module = module,
            ActionName = action,
            Parameters = parameters
                .Select(p => new global::app.Data.@this(p.name, p.value))
                .ToList()
        };
    }

    /// <summary>Wraps a typed value in Data.@this&lt;T&gt; for direct action construction in tests.</summary>
    public static global::app.Data.@this<T> D<T>(T value) => new("", value);

    /// <summary>Wraps an untyped value in Data.@this for direct action construction in tests.</summary>
    public static global::app.Data.@this D(object? value) => new("", value);
}
