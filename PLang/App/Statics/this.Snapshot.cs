using System.Collections.Concurrent;

namespace App.Statics;

public sealed partial class @this : ISnapshotted
{
    /// <summary>
    /// Captures the bag tree as a Dictionary<string, Dictionary<string, object?>>.
    /// Values are emitted by reference — Statics is provisional (see todos.md), so
    /// the value-shape contract here matches what callers already store.
    /// </summary>
    public void Capture(Snapshot.@this s)
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
    public static void Restore(Snapshot.@this s, Actor.Context.@this ctx)
    {
        var target = ctx.App.Statics;
        target._bags.Clear();
        var snap = s.Read<Dictionary<string, Dictionary<string, object?>>>("bags");
        if (snap == null) return;
        foreach (var (key, inner) in snap)
        {
            var bag = target.GetBag(key);
            foreach (var (k, v) in inner) bag[k] = v;
        }
    }
}
