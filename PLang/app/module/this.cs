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

    // Action elements cached — the class-zoom face on the .pr action host, minted once off
    // the registry's index and living as long as this element (which the registry drops on
    // its own mutation). The list wrapper mints fresh per ask over the same cached elements.
    private System.Collections.Generic.List<global::app.goal.steps.step.actions.action.@this>? _actions;

    /// <summary>The module's actions as the NATIVE plang list — filterable by the list module,
    /// renderable by templates.</summary>
    public global::app.type.item.list.@this Actions
        => new((_actions ??= _list.GetActions(Name)
                    .Select(a => new global::app.goal.steps.step.actions.action.@this
                    {
                        Module = Name,
                        ActionName = a,
                        Context = _list.App.System.Context,   // catalog-zoom — the leaf reaches its handler back through this
                    })
                    .ToList())
               .Select(a => (object?)a).ToList(),
            _list.App.System.Context);
}
