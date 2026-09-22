using System.Collections.Concurrent;

namespace app.Statics;

public sealed partial class @this : ISnapshot
{
    /// <summary>
    /// Captures the bag tree as a Dictionary<string, Dictionary<string, object?>>.
    /// Values are emitted by reference — Statics is provisional (see todos.md), so
    /// the value-shape contract here matches what callers already store.
    /// </summary>
    public void Capture(global::app.snapshot.@this s)
    {
        var snap = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, bag) in _bags)
        {
            var inner = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in bag) inner[k] = v;
            snap[key] = inner;
        }
        s.Write("bags", snap);
    }

    /// <summary>
    /// Replaces the live App's Statics bag tree with the captured one.
    /// </summary>
    public static async System.Threading.Tasks.Task Restore(global::app.snapshot.@this s, global::app.actor.context.@this context)
    {
        var target = context.App.Statics;
        target._bags.Clear();
        var entry = s.Entries.Get("bags");
        if (entry == null) return;

        // The entry is a dict of dicts of values. What Statics puts BACK in its own storage is
        // Statics' problem: that storage is itself an untyped bag — the same disease one level
        // down — so the values land there as the items they are until Statics is typed.
        var bags = await entry.Value<global::app.type.item.dict.@this>();
        foreach (var outer in bags.Entries)
        {
            var bag = target.GetBag(outer.Name);
            foreach (var inner in (await outer.Value<global::app.type.item.dict.@this>()).Entries)
                bag[inner.Name] = inner.Peek();
        }
    }

}
