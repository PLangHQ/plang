namespace PLang.Tests;

/// <summary>
/// Test-only helper that stamps the authored-template flag on an action's
/// %ref%-bearing parameters, mirroring what the plang builder does at leaf level
/// (<c>app.module.builder.code.Default.NormalizeParameterTypes</c>, the template
/// loop): a parameter whose raw string face carries a <c>%var%</c> hole is
/// declared with <c>type.template = "plang"</c> so it resolves live at dispatch.
/// Tests author actions the way the builder does; the .pr carries this flag.
/// </summary>
public static class TemplateStamp
{
    public static void Apply(global::app.goal.steps.step.actions.action.@this action)
    {
        Stamp(action.Parameters);
        if (action.Defaults != null) Stamp(action.Defaults);
        foreach (var modifier in action.Modifiers) Apply(modifier);
    }

    private static void Stamp(IEnumerable<global::app.data.@this> parameters)
    {
        foreach (var p in parameters)
        {
            var item = p.Peek();
            var raw = item.RawText;
            if (raw != null)
            {
                // Leaf: flag the declared type so the item rebuilds as a template
                // (Declare re-runs type.Build, which re-kinds a %ref% text to Template="plang").
                if (global::app.type.text.@this.HasVariable(raw))
                {
                    var t = p.Type;
                    p.Declare(new global::app.type.@this(t?.Name ?? "object", t?.Kind, t?.Strict ?? false, "plang"));
                }
                continue;
            }
            // Container: type.Build holds a container as-is (the flag is not applied
            // through Declare), so rebuild it as a template-flagged container with
            // flagged %ref% leaves — the shape a %ref%-bearing container has on the wire.
            var stamped = StampItem(item, p.Context);
            if (stamped != null && !ReferenceEquals(stamped, item))
                p.SetValueDirect(stamped);
        }
    }

    // The template-flagged form of a built value: null when nothing needs stamping,
    // a stamped copy otherwise. Mirrors the wire's authored container read.
    private static global::app.type.item.@this? StampItem(
        global::app.type.item.@this instance, global::app.actor.context.@this context)
    {
        switch (instance)
        {
            case global::app.type.text.@this t:
                return t.Template == null && global::app.type.text.@this.HasVariable(t.ToString())
                    ? new global::app.type.text.@this(t.ToString(), "plang") { Kind = t.Kind }
                    : null;

            case global::app.type.list.@this l when l.Template == null:
            {
                var items = l.Items;   // materialize once — entries rebind in place
                bool any = false;
                foreach (var entry in items) any |= StampEntry(entry, context);
                return any ? new global::app.type.list.@this(items) { Template = "plang", Context = context } : null;
            }

            case global::app.type.dict.@this d when d.Template == null:
            {
                var entries = d.Entries;
                bool any = false;
                foreach (var entry in entries) any |= StampEntry(entry, context);
                if (!any) return null;
                var stampedDict = new global::app.type.dict.@this { Template = "plang", Context = context };
                foreach (var entry in entries) stampedDict.Set(entry);
                return stampedDict;
            }

            default:
                return null;
        }
    }

    // Rebinds one container entry to its stamped form. True when the entry holds a stamp.
    private static bool StampEntry(global::app.data.@this entry, global::app.actor.context.@this context)
    {
        var inner = entry.Peek();
        var stamped = StampItem(inner, context);
        if (stamped != null && !ReferenceEquals(stamped, inner))
            entry.SetValueDirect(stamped);
        return (stamped ?? inner).Template != null;
    }

    /// <summary>
    /// Builds a Data whose CONTAINER value carries the authored-template flag
    /// explicitly — the shape a %ref%-bearing container has once it rides the wire:
    /// <c>Template = "plang"</c> on the container AND on each %ref% text leaf, so
    /// both the render door (<c>Value</c>) and the canonical door (<c>AsCanonical</c>,
    /// which reads the container's own Template) resolve the nested refs. Scalar
    /// %ref% values carry the flag via a flagged <c>text</c> type at the call site;
    /// this helper is for list/dict values only.
    /// </summary>
    public static global::app.data.@this Container(
        string name, object? raw, global::app.actor.context.@this context)
        => new global::app.data.@this(name, Build(raw, context), context: context);

    private static global::app.type.item.@this Build(object? raw, global::app.actor.context.@this context)
    {
        switch (raw)
        {
            case string s when global::app.type.text.@this.HasVariable(s):
                return new global::app.type.text.@this(s, "plang");

            case IDictionary<string, object?> d:
            {
                var dict = new global::app.type.dict.@this { Template = "plang", Context = context };
                foreach (var kv in d)
                    dict.Set(new global::app.data.@this(kv.Key, Build(kv.Value, context), context: context));
                return dict;
            }

            case System.Collections.IEnumerable e and not string:
            {
                var items = new List<global::app.data.@this>();
                foreach (var el in e)
                    items.Add(new global::app.data.@this("", Build(el, context), context: context));
                return new global::app.type.list.@this(items) { Template = "plang", Context = context };
            }

            // A literal leaf (holeless string, number, bool) — built as its plain type.
            default:
                return global::app.type.@this.Create(raw, context);
        }
    }
}
