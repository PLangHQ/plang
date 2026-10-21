namespace app.type.item.setting.action;

/// <summary>
/// An action's settings — <c>%!llm.query.setting%</c>, made by the action for an asker: its options are its properties
/// (<c>%!llm.query.setting.cache%</c>), each as the action-param seam reads it (this run's — the action's, then
/// the module's — the saved rows), else the action's default; a write is checked against them. What
/// <c>save %!llm.query.setting%</c> stores is this run's values at its address.
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>A settings class is a type but never LLM vocabulary (774) — kept out of the catalog.</summary>
    public static bool Internal => true;

    private readonly global::app.goal.step.action.@this _action;

    /// <summary>The settings of the action's module — where an option the action's own don't set is read next.</summary>
    internal global::app.type.item.setting.module.@this Module { get; }

    /// <param name="action">The action as the catalog has it; it and its module's settings are born holding the app's
    /// types, the app its module is in.</param>
    public @this(global::app.goal.step.action.@this action) : this(action, action.Module.App.type.list) { }

    private @this(global::app.goal.step.action.@this action, global::app.type.list.@this types)
        : this(action, new global::app.type.item.setting.module.@this(action.Module.Name, types[typeof(global::app.type.item.setting.module.@this)]),
            types[typeof(@this)]) { }

    private @this(global::app.goal.step.action.@this action, global::app.type.item.setting.module.@this module, global::app.type.@this type)
        : base(action.Address, type)
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
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Set(string key, bool isIndex,
        object? value, global::app.actor.context.@this context)
    {
        if (_action.Property[key] is not { } option)
            return context.Error(new global::app.error.@this($"action '{Path}' has no option '{key}'", "NoOption", 404));
        // born as the option's type — a record from its dict, the members it leaves out at their defaults; a value
        // the option can't take is refused before this run holds it
        var raw = value is global::app.data.@this given ? await given.Value() : value;
        var binding = new global::app.data.@this(key, raw, context: context);
        if (option.Type.Make(raw, binding) is not { } made)
            return context.Error(binding.Error!);
        await Write(key, made, context);
        return context.Ok(this);
    }
}
