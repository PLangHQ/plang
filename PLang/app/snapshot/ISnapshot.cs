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

    /// <summary>
    /// Reconstructs the subtree <paramref name="section"/> from the wire cursor
    /// <paramref name="io"/>, casting each entry back to the concrete type the
    /// subsystem owns. The result is the same in-memory shape <see cref="Capture"/>
    /// produced, so the existing <see cref="Restore"/> consumes it unchanged.
    ///
    /// <para>There is no write-side counterpart here: the snapshot serializes
    /// itself generically via <see cref="@this.ToTree"/> (it owns its own tree),
    /// and a domain value that isn't tree-native owns its node via
    /// <see cref="ITreeValue"/>. Read stays explicit because rebuilding a typed
    /// section from an untyped parsed tree needs the owner's type knowledge.</para>
    /// </summary>
    static abstract void Read(Io io, @this section);
}
