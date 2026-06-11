namespace app.goal.steps.step.actions.action;

public sealed partial class @this
{
    /// <summary>
    /// Stamps this action's authored parameter values as live templates — the
    /// deterministic half of the template contract (detection is code, not the
    /// LLM). A builder-authored text containing <c>%ref%</c> holes is rebound
    /// to a stamped copy (<c>Template = "plang"</c>); a container with refs
    /// inside is rebuilt stamped, so use-time knows something needs rendering
    /// without walking. Runs only at the authored seams (.pr load,
    /// <see cref="FromWire"/>) — runtime input never passes here, so a user
    /// string <c>"%secret%"</c> is never stamped and prints literally.
    /// Idempotent: an already-stamped value is left alone.
    /// </summary>
    public void StampTemplates()
    {
        foreach (var p in Parameters) Stamp(p);
        if (Defaults != null) foreach (var p in Defaults) Stamp(p);
        foreach (var m in Modifiers) m.StampTemplates();
    }

    private static void Stamp(global::app.data.@this slot)
    {
        if (slot.Instance is not { } instance) return;
        if (Stamped(instance) is { } stamped && !ReferenceEquals(stamped, instance))
            slot.SetValueDirect(stamped);
    }

    /// <summary>
    /// The stamped form of an authored value: the instance itself when there
    /// is nothing to stamp, a stamped copy otherwise. Containers recurse —
    /// nested entry values restamp in place (entry Data rebind), and the
    /// container itself is rebuilt stamped when anything inside has holes.
    /// </summary>
    private static global::app.type.item.@this? Stamped(global::app.type.item.@this instance)
    {
        switch (instance)
        {
            case global::app.type.text.@this t:
                if (t.Template != null || !HasRef(t.Value)) return t;
                return new global::app.type.text.@this(t.Value) { Kind = t.Kind, Template = "plang" };

            case global::app.type.list.@this l:
            {
                if (l.Template != null) return l;
                bool any = false;
                foreach (var entry in l.Items) any |= StampEntry(entry);
                if (!any) return l;
                var stampedList = new global::app.type.list.@this(l.Items) { Template = "plang", Context = l.Context };
                return stampedList;
            }

            case global::app.type.dict.@this d:
            {
                if (d.Template != null) return d;
                bool any = false;
                foreach (var entry in d.Entries) any |= StampEntry(entry);
                if (!any) return d;
                var stampedDict = new global::app.type.dict.@this { Template = "plang", Context = d.Context };
                foreach (var entry in d.Entries) stampedDict.Set(entry);
                return stampedDict;
            }

            default:
                return instance;
        }
    }

    // Restamps one container entry in place (the entry Data rebinds to the
    // stamped value). True when the entry holds (or now holds) a stamp.
    private static bool StampEntry(global::app.data.@this entry)
    {
        if (entry.Instance is not { } inner) return false;
        var stamped = Stamped(inner);
        if (stamped != null && !ReferenceEquals(stamped, inner))
            entry.SetValueDirect(stamped);
        return (stamped ?? inner).Template != null;
    }

    private static bool HasRef(string value)
        => System.Text.RegularExpressions.Regex.IsMatch(value, "%[^%]+%");
}
