using System.Collections.Concurrent;

namespace app.Statics;

/// <summary>
/// App-scoped key/value store. Each top-level key holds its own bag of
/// name → value. Persists for the lifetime of the App; survives goal calls.
/// Implements <see cref="app.snapshot.ISnapshot"/> so it round-trips with
/// the rest of the App tree on snapshot/restore.
///
/// TODO: replace with goal-backed dynamic property (see Documentation/Runtime2/todos.md).
/// This @this exists today to give the data structure a name and an OBP shape;
/// behaviour matches the prior inline `App._statics`.
/// </summary>
public sealed partial class @this
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object?>> _bags =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the bag for <paramref name="key"/>, creating an empty one on first access.
    /// Modules use this for app-lifetime mutable state that doesn't fit on Variables
    /// (e.g. background listener registries).
    /// </summary>
    public ConcurrentDictionary<string, object?> GetBag(string key) =>
        _bags.GetOrAdd(key, _ => new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase));

    /// <summary>True when no bag has been created.</summary>
    public bool IsEmpty => _bags.IsEmpty;

    /// <summary>Direct read-only view used by snapshot/restore. Not part of public API.</summary>
    internal IReadOnlyDictionary<string, ConcurrentDictionary<string, object?>> Bags => _bags;
}
