namespace app.snapshot;

/// <summary>
/// Marker for any subsystem `@this` that participates in App snapshot/restore.
/// The type system is the classifier — implementing this interface puts the
/// subsystem in the snapshot-and-restore bucket; not implementing it keeps it
/// in the reconstruct-on-build bucket (Modules, Goals, Channels, Cache, …).
///
/// Each implementer captures values into its own <see cref="@this"/> subtree
/// and reconstructs from that subtree on Restore. References across subsystems
/// are by name (the way PLang already works at runtime) — Restore never depends
/// on inter-subsystem ordering or pointer fixup.
/// </summary>
public interface ISnapshot
{
    /// <summary>
    /// Writes this subsystem's state into <paramref name="s"/>. The subsystem
    /// owns its subtree's wire shape — callers never inspect the entries.
    /// </summary>
    void Capture(@this s);

    /// <summary>
    /// Static factory: rebuilds the subsystem from <paramref name="s"/> into the
    /// live App reachable via <paramref name="context"/>. Hard-errors on referent-integrity
    /// violations (unresolvable name, hash mismatch, missing source). No silent fallback.
    /// </summary>
    static abstract void Restore(@this s, actor.context.@this context);
}
