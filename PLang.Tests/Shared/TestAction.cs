namespace PLang.Tests;

/// <summary>
/// Builds Action.@this objects for tests so they go through the same
/// ExecuteAsync → __Resolve → Run() pipeline as runtime .pr dispatch.
/// </summary>
public static class TestAction
{
    public static global::app.goal.steps.step.actions.action.@this Create(string module, string action,
        params (string name, object? value)[] parameters)
    {
        var act = new global::app.goal.steps.step.actions.action.@this
        {
            Module = module,
            ActionName = action,
            Parameters = parameters
                .Select(p => new global::app.data.@this(p.name, p.value))
                .ToList()
        };
        // Tests author actions the way the builder does — same template seam
        // the .pr load applies, so %ref% parameters resolve live at dispatch.
        act.StampTemplates();
        return act;
    }

    /// <summary>Wraps a typed value in data.@this&lt;T&gt; for direct action construction in tests.</summary>
    public static global::app.data.@this<T> D<T>(T value) where T : global::app.type.item.@this, global::app.type.item.ICreate<T> => new("", value);

    /// <summary>Wraps an untyped value in data.@this for direct action construction in tests.</summary>
    public static global::app.data.@this D(object? value) => new("", value);
}
