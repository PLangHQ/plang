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
    public static void Restore(global::app.snapshot.@this s, global::app.actor.context.@this context)
    {
        var target = context.App.Statics;
        target._bags.Clear();
        var snap = s.Read<Dictionary<string, Dictionary<string, object?>>>("bags");
        if (snap == null) return;
        foreach (var (key, inner) in snap)
        {
            var bag = target.GetBag(key);
            foreach (var (k, v) in inner) bag[k] = v;
        }
    }

    // Statics is provisional: bag values are object?, so non-scalar values
    // rehydrate as JsonElement. Scalars round-trip cleanly, which is what the
    // current callers store.
    public static void Write(global::app.snapshot.@this section, global::app.snapshot.Io io)
        => io.Put("bags", section.Read<Dictionary<string, Dictionary<string, object?>>>("bags")
            ?? new(StringComparer.OrdinalIgnoreCase));

    public static void Read(global::app.snapshot.Io io, global::app.snapshot.@this section)
        => section.Write("bags", io.Get<Dictionary<string, Dictionary<string, object?>>>("bags")
            ?? new(StringComparer.OrdinalIgnoreCase));
}
