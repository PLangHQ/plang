namespace app.type.item.setting.action;

/// <summary>
/// An action's settings — <c>%!llm.query.setting%</c>: its options are its properties
/// (<c>%!llm.query.setting.cache%</c>), each as the action-param seam reads it (this run's — the action's, then
/// the module's — the saved rows), else the action's default. What <c>save %!llm.query.setting%</c> stores is the
/// action with them.
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    private readonly global::app.goal.step.action.@this _action;

    /// <summary>The settings of the action's module — where an option the action's own don't set is read next.</summary>
    internal global::app.type.item.setting.module.@this Module { get; }

    /// <param name="action">The action as the catalog has it.</param>
    public @this(global::app.goal.step.action.@this action) : this(action, new global::app.type.item.setting.module.@this(action.Module.Name)) { }

    private @this(global::app.goal.step.action.@this action, global::app.type.item.setting.module.@this module)
        : base($"{action.Module.Name}.{action.Name}.setting")
    {
        _action = action;
        Module = module;
    }

    /// <summary>One of the action's options, as this run and the saved rows set it, else its default.</summary>
    protected override async System.Threading.Tasks.ValueTask<global::app.data.@this> Next(global::app.data.@this parent, string key)
    {
        if (_action.Property[key] is not { } option) return parent.Context.NotFound(key);
        var set = await parent.Context.Setting.Get(_action, option.Name);
        return set.IsInitialized ? set : new global::app.data.@this(key, option.Default, parent: parent);
    }

    /// <summary>This run's value for one of the action's options (<c>set %!http.request.setting.timeout% = 5</c>),
    /// where the action-param seam reads it.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(string key, bool isIndex,
        object? value, global::app.actor.context.@this context)
    {
        if (_action.Property[key] == null)
            throw new System.NotSupportedException($"action '{Path}' has no option '{key}'");
        await Write(key, value, context);
        return this;
    }
}
