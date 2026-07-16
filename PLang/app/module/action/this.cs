namespace app.module.action;

/// <summary>
/// The flat action collection — every registered action AND modifier across all modules,
/// reached at <c>app.module.action</c> and enumerated at <c>.list</c>
/// (<c>%!app.module.action.list%</c>). The module collection owns per-module selection
/// (<c>app.module["file"]["read"]</c>); this node exists only to enumerate the cross-module
/// union the builder catalog walks. A HOST — never authored, navigated by reflection.
///
/// <para>Not to be confused with the <c>list</c> action MODULE at the sibling namespace
/// <c>app.module.action.list</c>: action modules are dispatched, not navigated, so they put
/// no object at this navigation path — it is the flat catalog's alone.</para>
/// </summary>
public sealed class @this
{
    private readonly global::app.module.list.@this _modules;

    internal @this(global::app.module.list.@this modules) => _modules = modules;

    /// <summary>Every module's actions + modifiers as the NATIVE plang list — the flat catalog
    /// surface the builder templates filter with <c>where … Name in …</c>. A fresh, cheap wrapper
    /// per ask over the module elements' own cached action elements.</summary>
    public global::app.type.item.list.@this list
    {
        get
        {
            var items = new System.Collections.Generic.List<object?>();
            foreach (var name in _modules.Names)
            {
                var element = _modules[name];
                foreach (var a in element.Actions.Items) items.Add(a.Peek());
                foreach (var m in element.Modifiers.Items) items.Add(m.Peek());
            }
            return new global::app.type.item.list.@this(items, _modules.App.System.Context);
        }
    }
}
