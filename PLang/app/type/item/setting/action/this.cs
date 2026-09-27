namespace app.type.item.setting.action;

/// <summary>
/// An action's settings — <c>%!llm.query%</c>: its options are its properties (<c>%!llm.query.cache%</c>),
/// each as the action-param seam reads it (this run's — the action's, then the module's — the saved rows),
/// else the action's default. What <c>save %!llm.query%</c> stores is the action with them.
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    private readonly global::app.goal.step.action.@this _action;

    /// <param name="action">The action as the catalog has it.</param>
    public @this(global::app.goal.step.action.@this action) : base($"{action.Module.Name}.{action.Name}") => _action = action;

    /// <summary>One of the action's options, as this run and the saved rows set it, else its default.</summary>
    protected override async System.Threading.Tasks.ValueTask<global::app.data.@this> Next(global::app.data.@this parent, string key)
    {
        if (_action.Property.FirstOrDefault(p => string.Equals(p.Name, key, System.StringComparison.OrdinalIgnoreCase)) is not { } option)
            return parent.Context.NotFound(key);
        var set = await parent.Context.Setting.Get([$"{Path}.{option.Name}", $"{_action.Module.Name}.{option.Name}"]);
        return set.IsInitialized ? set : new global::app.data.@this(key, option.Default, parent: parent);
    }
}
