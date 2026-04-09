namespace PLang.Tests;

/// <summary>
/// Builds Action.@this objects for tests so they go through the same
/// ExecuteAsync → __Resolve → Run() pipeline as runtime .pr dispatch.
/// </summary>
public static class TestAction
{
    public static global::App.Goals.Goal.Steps.Step.Actions.Action.@this Create(string module, string action,
        params (string name, object? value)[] parameters)
    {
        return new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
        {
            Module = module,
            ActionName = action,
            Parameters = parameters
                .Select(p => new global::App.Data.@this(p.name, p.value))
                .ToList()
        };
    }
}
