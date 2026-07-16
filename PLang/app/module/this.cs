using System.Reflection;

namespace app.module;

/// <summary>
/// One module — a HOST (never authored, never created from values; item⟺ICreate rules
/// it out as a plang type). Carried as <c>clr(module)</c>, navigated by reflection, read
/// by templates through its own doors. The element at the concept node <c>app.module</c>;
/// the collection is <c>app.module.list.@this</c>, which owns selection and lifecycle and
/// mints these elements.
/// </summary>
public sealed class @this
{
    private readonly list.@this _list;

    /// <summary>The module name — "file", "variable", "list".</summary>
    public string Name { get; }

    internal @this(string name, list.@this list)
    {
        Name = name;
        _list = list;
    }

    /// <summary>The handler CLR type for one of this module's actions — the owner's answer, handed
    /// TRANSIENTLY to the reflection leaf. The type lives in the registry index (keyed by the
    /// identity the action carries); it never rides on the action itself.</summary>
    internal System.Type? Handler(string actionName) => _list.GetActionType(Name, actionName);

    // Elements cached — the class-zoom face on the .pr action host, minted ONCE off the registry
    // index and living as long as this element (the registry drops it on its own mutation). One
    // walk, two homes: [Modifier] routes each name to its role. The list wrappers mint fresh per
    // ask over the same cached elements.
    private System.Collections.Generic.List<global::app.goal.steps.step.actions.action.@this>? _actions;
    private System.Collections.Generic.List<global::app.goal.steps.step.actions.action.modifier.@this>? _modifiers;

    private void Mint()
    {
        _actions = new(); _modifiers = new();
        var ctx = _list.App.System.Context;
        foreach (var name in _list.GetActions(Name))
        {
            var order = Handler(name)?.GetCustomAttribute<global::app.module.ModifierAttribute>()?.Order;
            if (order != null)
                _modifiers.Add(new global::app.goal.steps.step.actions.action.modifier.@this
                    { Module = Name, ActionName = name, Order = order.Value, Context = ctx });
            else
                _actions.Add(new global::app.goal.steps.step.actions.action.@this
                    { Module = Name, ActionName = name, Context = ctx });
        }
    }

    /// <summary>The module's standalone actions as the NATIVE plang list — modifiers are a separate
    /// home (structural, not a flag). Filterable by the list module, renderable by templates.</summary>
    public global::app.type.item.list.@this Actions
    {
        get { if (_actions == null) Mint(); return new(_actions!.Select(a => (object?)a).ToList(), _list.App.System.Context); }
    }

    /// <summary>The module's modifiers as the NATIVE plang list — the catalog's "# Modifiers"
    /// section renders from here; the type IS the role, no boolean.</summary>
    public global::app.type.item.list.@this Modifiers
    {
        get { if (_modifiers == null) Mint(); return new(_modifiers!.Select(m => (object?)m).ToList(), _list.App.System.Context); }
    }

    /// <summary>Select one catalog element by action name — action OR modifier; the type answers
    /// the role. Null when the name isn't in this module.</summary>
    public global::app.goal.steps.step.actions.action.@this? this[string actionName]
    {
        get
        {
            if (_actions == null) Mint();
            return _actions!.FirstOrDefault(a => string.Equals(a.ActionName, actionName, System.StringComparison.OrdinalIgnoreCase))
                ?? (global::app.goal.steps.step.actions.action.@this?)_modifiers!.FirstOrDefault(m => string.Equals(m.ActionName, actionName, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
