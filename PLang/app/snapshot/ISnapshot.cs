namespace app.snapshot;

/// <summary>
/// A part of the App that snapshots and restores ITSELF. The type system is the classifier —
/// implementing this interface puts the owner in the snapshot-and-restore bucket; not implementing
/// it keeps it in the reconstruct-on-build bucket (Modules, Goals, Channels, Cache, …).
///
/// The owner names its own section and owns both halves of its shape: <see cref="Capture"/> writes
/// the section, <see cref="Restore"/> reads it back into THIS instance (the destination's live
/// owner). References across owners are by name (the way PLang already works at runtime) — Restore
/// never depends on pointer fixup.
/// </summary>
public interface ISnapshot
{
    /// <summary>The name of this owner's section in the snapshot.</summary>
    string Section { get; }

    /// <summary>Writes this owner's state into its section <paramref name="s"/>.</summary>
    void Capture(@this s);

    /// <summary>
    /// Reads this owner's section <paramref name="s"/> back into this instance. Hard-errors on
    /// referent-integrity violations (unresolvable name, hash mismatch, missing source). No
    /// silent fallback.
    /// </summary>
    /// <remarks>Async because entries are plang values and the typed ask that reads them is async —
    /// the same door every other value read goes through. Nothing here lowers to CLR.</remarks>
    System.Threading.Tasks.Task Restore(@this s, actor.context.@this context);
}
