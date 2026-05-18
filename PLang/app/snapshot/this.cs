namespace app.snapshot;

/// <summary>
/// Typed read/write surface for a subsystem snapshot. A snapshot is a tree of
/// named sections; each section is itself a `@this` so a subsystem with nested
/// `ISnapshot` properties (e.g. App owning Variables, Errors, …) can give
/// each child its own subtree without leaking storage to the children.
///
/// The wire shape is the App tree — that's the OBP win the design hangs on.
/// Subsystems write entries via <see cref="Write{T}"/> and read via
/// <see cref="Read{T}"/>; the underlying storage is an implementation detail.
/// </summary>
public sealed class @this
{
    private readonly Dictionary<string, @this> _sections =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the named subsection, creating it if missing. Subsystems hand the
    /// returned subtree to their nested ISnapshot children — each owns its scope.
    /// </summary>
    public @this Section(string name)
    {
        if (_sections.TryGetValue(name, out var existing)) return existing;
        var created = new @this();
        _sections[name] = created;
        return created;
    }

    /// <summary>True if a section with this name was captured.</summary>
    public bool HasSection(string name) => _sections.ContainsKey(name);

    /// <summary>Names of all captured subsections, for App.Restore dispatch.</summary>
    public IReadOnlyCollection<string> SectionNames => _sections.Keys;

    /// <summary>Writes a typed entry. Overwrites any prior value at the same key.</summary>
    public void Write<T>(string key, T value) => _entries[key] = value;

    /// <summary>
    /// Reads a typed entry. Returns default(T) when missing — callers that need
    /// presence checks use <see cref="Has"/>.
    /// </summary>
    public T? Read<T>(string key) =>
        _entries.TryGetValue(key, out var v) && v is T typed ? typed : default;

    /// <summary>True if an entry with this key was written.</summary>
    public bool Has(string key) => _entries.ContainsKey(key);

    /// <summary>Number of entries directly on this section (excludes nested sections).</summary>
    public int EntryCount => _entries.Count;
}
